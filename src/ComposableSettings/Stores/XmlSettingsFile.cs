using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using ComposableSettings.Configuration;
using ComposableSettings.Runtime;
using ComposableSettings.Static;

namespace ComposableSettings.Stores;

public class XmlSettingsFile : IComponentSettingsProvider
{
    private readonly XDocument _document;

    public XmlSettingsFile(SettingsFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        SettingsFilePath = SettingsPathResolver.ResolveFilePath(options);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        _document = LoadOrCreateDocument(SettingsFilePath);
    }

    /// <summary>
    ///     Creates an instance from a fully-resolved file path instead of option-based resolution.
    ///     The directory is created if it does not exist.
    /// </summary>
    public XmlSettingsFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        SettingsFilePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        _document = LoadOrCreateDocument(SettingsFilePath);
    }

    public string SettingsFilePath { get; }

    public TSettings Get<TSettings>(SettingsNodePath path)
        where TSettings : class, new()
    {
        ArgumentNullException.ThrowIfNull(path);

        var element = FindElement(path.Segments);
        if (element is null)
            return new TSettings();

        return TryDeserialize<TSettings>(element, out var settings) ? settings : new TSettings();
    }

    public void Set<TSettings>(SettingsNodePath path, TSettings value)
        where TSettings : class, new()
    {
        Write(path, value);
        Flush();
    }

    /// <summary>
    ///     Tries to load and deserialize settings at <paramref name="path" />.
    ///     Returns <c>false</c> (and a fresh default) if the element is missing or cannot be deserialized.
    ///     Does not touch the file on disk.
    /// </summary>
    public bool TryLoad<TSettings>(SettingsNodePath path, out TSettings settings)
        where TSettings : class, new()
    {
        ArgumentNullException.ThrowIfNull(path);

        var element = FindElement(path.Segments);
        if (element is not null) return TryDeserialize(element, out settings);
        settings = new TSettings();
        return false;
    }

    /// <summary>
    ///     Writes settings to the in-memory document without saving to disk.
    ///     Call <see cref="Flush" /> when all writes in a batch are done.
    /// </summary>
    private void Write<TSettings>(SettingsNodePath path, TSettings value)
        where TSettings : class, new()
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(value);

        WriteElement(path.Segments, value);
    }

    /// <summary>Saves the in-memory document to disk.</summary>
    private void Flush()
    {
        _document.Save(SettingsFilePath);
    }

    private static XDocument LoadOrCreateDocument(string filePath)
    {
        if (!File.Exists(filePath))
            return new XDocument(new XElement("settings"));

        try
        {
            var document = XDocument.Load(filePath);
            return document.Root?.Name.LocalName == "settings"
                ? document
                : new XDocument(new XElement("settings"));
        }
        catch
        {
            return new XDocument(new XElement("settings"));
        }
    }

    private XElement? FindElement(IReadOnlyList<string> segments)
    {
        var current = _document.Root;
        foreach (var segment in segments)
        {
            current = current?.Element(segment);
            if (current is null)
                return null;
        }

        return current;
    }

    private XElement GetOrCreateElement(IReadOnlyList<string> segments)
    {
        var current = _document.Root!;
        foreach (var segment in segments)
        {
            var next = current.Element(segment);
            if (next is null)
            {
                next = new XElement(segment);
                current.Add(next);
            }

            current = next;
        }

        return current;
    }

    private void WriteElement<TSettings>(IReadOnlyList<string> segments, TSettings settings)
        where TSettings : class, new()
    {
        var target = GetOrCreateElement(segments);
        var serialized = Serialize(settings, target.Name.LocalName);

        target.RemoveAttributes();
        target.RemoveNodes();

        foreach (var attribute in serialized.Attributes())
            target.Add(new XAttribute(attribute));

        target.Add(serialized.Nodes());
    }

    private static XElement Serialize<TSettings>(TSettings settings, string rootElementName)
        where TSettings : class, new()
    {
        var serializer = new XmlSerializer(typeof(TSettings), new XmlRootAttribute(rootElementName));
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add(string.Empty, string.Empty);

        using var textWriter = new StringWriter(CultureInfo.InvariantCulture);
        using (var xmlWriter = XmlWriter.Create(textWriter, new XmlWriterSettings
               {
                   OmitXmlDeclaration = true,
                   Indent = true
               }))
        {
            serializer.Serialize(xmlWriter, settings, namespaces);
        }

        return XElement.Parse(textWriter.ToString());
    }

    private static bool TryDeserialize<TSettings>(XElement element, out TSettings settings)
        where TSettings : class, new()
    {
        try
        {
            var serializer = new XmlSerializer(typeof(TSettings), new XmlRootAttribute(element.Name.LocalName));
            using var reader = element.CreateReader();
            var value = serializer.Deserialize(reader);
            if (value is TSettings typed)
            {
                ReplaceListPropertiesFromXml(element, typed);
                settings = typed;
                return true;
            }
        }
        catch
        {
            // ignored
        }

        settings = new TSettings();
        return false;
    }

    private static void ReplaceListPropertiesFromXml<TSettings>(XElement element, TSettings settings)
        where TSettings : class, new()
    {
        foreach (var property in typeof(TSettings).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.PropertyType.IsGenericType)
                continue;

            var definition = property.PropertyType.GetGenericTypeDefinition();
            var isList = definition == typeof(List<>) && property.CanWrite;
            var isObservable = definition == typeof(ObservableCollection<>);
            if (!isList && !isObservable)
                continue;

            var itemType = property.PropertyType.GetGenericArguments()[0];
            var arrayName = property.GetCustomAttribute<XmlArrayAttribute>()?.ElementName;
            if (string.IsNullOrWhiteSpace(arrayName))
                arrayName = property.Name;

            var arrayElement = element.Element(arrayName);
            if (arrayElement is null)
                continue;

            var itemName = property.GetCustomAttribute<XmlArrayItemAttribute>()?.ElementName;
            var items = string.IsNullOrWhiteSpace(itemName)
                ? arrayElement.Elements()
                : arrayElement.Elements(itemName);

            if (isList)
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
                foreach (var itemElement in items)
                    list.Add(ConvertListItem(itemElement, itemType));

                property.SetValue(settings, list);
            }
            else
            {
                // ObservableCollection is get-only: mutate the existing instance in place
                // (preserves the model's CollectionChanged subscription) and REPLACE its
                // contents so defaults are not duplicated with the persisted items.
                if (property.GetValue(settings) is not IList target)
                    continue;

                target.Clear();
                foreach (var itemElement in items)
                    target.Add(ConvertListItem(itemElement, itemType));
            }
        }
    }

    private static object ConvertListItem(XElement element, Type itemType)
    {
        if (itemType == typeof(string))
            return element.Value;
        if (itemType.IsEnum)
            return Enum.Parse(itemType, element.Value);
        if (typeof(IConvertible).IsAssignableFrom(itemType))
            return Convert.ChangeType(element.Value, itemType, CultureInfo.InvariantCulture);

        // Complex item type: deserialize the element itself (its child properties).
        var serializer = new XmlSerializer(itemType, new XmlRootAttribute(element.Name.LocalName));
        using var reader = element.CreateReader();
        return serializer.Deserialize(reader)!;
    }
}