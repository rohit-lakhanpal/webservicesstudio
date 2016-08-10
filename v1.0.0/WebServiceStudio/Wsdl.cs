using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace WebServiceStudio
{
    using Microsoft.CSharp;
    using Microsoft.VisualBasic;
    using System;
    using System.CodeDom;
    using System.CodeDom.Compiler;
    using System.Collections;
    using System.Collections.Specialized;
    using System.Data;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Web.Services;
    using System.Web.Services.Description;
    using System.Web.Services.Discovery;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;

    internal class Wsdl
    {
        private bool cancelled = false;
        private CodeDomProvider codeProvider = null;
        private CodeCompileUnit compileUnit;
        private static string duplicateSchema = "Warning: Ignoring duplicate schema description with TargetNamespace='{0}' from '{1}'.";
        private static string duplicateService = "Warning: Ignoring duplicate service description with TargetNamespace='{0}' from '{1}'.";
        private StringCollection paths = new StringCollection();
        private Assembly proxyAssembly;
        private string proxyCode = "<NOT YET>";
        private string proxyNamespace = "";
        private static string schemaValidationFailure = "Schema could not be validated. Class generation may fail or may produce incorrect results";
        private StringCollection wsdls = new StringCollection();
        private StringCollection xsds = new StringCollection();

        private void AddDocument(string path, object document, XmlSchemas schemas, ServiceDescriptionCollection descriptions)
        {
            var serviceDescription = document as ServiceDescription;
            if (serviceDescription != null)
            {
                if (descriptions[serviceDescription.TargetNamespace] == null)
                {
                    descriptions.Add(serviceDescription);
                    var w = new StringWriter();
                    var writer = new XmlTextWriter(w);
                    writer.Formatting = Formatting.Indented;
                    serviceDescription.Write(writer);
                    this.wsdls.Add(w.ToString());
                }
                else
                {
                    this.CheckPoint(MessageType.Warning, string.Format(duplicateService, serviceDescription.TargetNamespace, path));
                }
            }
            else
            {
                var schema = document as XmlSchema;
                if (schema != null)
                {
                    if (schemas[schema.TargetNamespace] == null)
                    {
                        schemas.Add(schema);
                        var writer3 = new StringWriter();
                        var writer4 = new XmlTextWriter(writer3);
                        writer4.Formatting = Formatting.Indented;
                        schema.Write(writer4);
                        this.xsds.Add(writer3.ToString());
                    }
                    else
                    {
                        this.CheckPoint(MessageType.Warning, string.Format(duplicateSchema, serviceDescription.TargetNamespace, path));
                    }
                }
            }
        }

        private static void AddElementAndType(XmlSchema schema, string baseXsdType, string ns)
        {
            var item = new XmlSchemaElement();
            item.Name = baseXsdType;
            item.SchemaTypeName = new XmlQualifiedName(baseXsdType, ns);
            schema.Items.Add(item);
            var type = new XmlSchemaComplexType();
            type.Name = baseXsdType;
            var content = new XmlSchemaSimpleContent();
            type.ContentModel = content;
            var extension = new XmlSchemaSimpleContentExtension();
            extension.BaseTypeName = new XmlQualifiedName(baseXsdType, "http://www.w3.org/2001/XMLSchema");
            content.Content = extension;
            schema.Items.Add(type);
        }

        private static void AddFakeSchemas(XmlSchema parent, XmlSchemas schemas)
        {
            if (schemas["http://www.w3.org/2001/XMLSchema"] == null)
            {
                var item = new XmlSchemaImport();
                item.Namespace = "http://www.w3.org/2001/XMLSchema";
                item.Schema = CreateFakeXsdSchema("http://www.w3.org/2001/XMLSchema", "schema");
                parent.Includes.Add(item);
            }
            if (schemas["http://schemas.xmlsoap.org/soap/encoding/"] == null)
            {
                var import2 = new XmlSchemaImport();
                import2.Namespace = "http://schemas.xmlsoap.org/soap/encoding/";
                import2.Schema = CreateFakeSoapEncodingSchema("http://schemas.xmlsoap.org/soap/encoding/", "Array");
                parent.Includes.Add(import2);
            }
            if (schemas["http://schemas.xmlsoap.org/wsdl/"] == null)
            {
                var import3 = new XmlSchemaImport();
                import3.Namespace = "http://schemas.xmlsoap.org/wsdl/";
                import3.Schema = CreateFakeWsdlSchema("http://schemas.xmlsoap.org/wsdl/");
                parent.Includes.Add(import3);
            }
        }

        private static void AddSimpleType(XmlSchema schema, string typeName, string baseXsdType)
        {
            var item = new XmlSchemaSimpleType();
            item.Name = typeName;
            var restriction = new XmlSchemaSimpleTypeRestriction();
            restriction.BaseTypeName = new XmlQualifiedName(baseXsdType, "http://www.w3.org/2001/XMLSchema");
            item.Content = restriction;
            schema.Items.Add(item);
        }

        public void Cancel()
        {
            this.cancelled = true;
        }

        private void ChangeBaseType(CodeCompileUnit compileUnit)
        {
            if ((this.WsdlProperties.ProxyBaseType != null) && (this.WsdlProperties.ProxyBaseType.Length != 0))
            {
                var type = Type.GetType(this.WsdlProperties.ProxyBaseType, true);
                foreach (CodeNamespace namespace2 in compileUnit.Namespaces)
                {
                    foreach (CodeTypeDeclaration declaration in namespace2.Types)
                    {
                        var flag = false;
                        foreach (CodeAttributeDeclaration declaration2 in declaration.CustomAttributes)
                        {
                            if (declaration2.Name == typeof(WebServiceBindingAttribute).FullName)
                            {
                                flag = true;
                                break;
                            }
                        }
                        if (flag)
                        {
                            declaration.BaseTypes[0] = new CodeTypeReference(type.FullName);
                        }
                    }
                }
            }
        }

        private void CheckPoint(MessageType status, string message)
        {
            if (!this.cancelled)
            {
                MainForm.ShowMessage(this, status, message);
            }
        }

        private static void CollectIncludes(XmlSchema schema, Hashtable includeSchemas)
        {
            var enumerator = schema.Includes.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = (XmlSchemaExternal) enumerator.Current;
                var schemaLocation = current.SchemaLocation;
                if (current is XmlSchemaImport)
                {
                    current.SchemaLocation = null;
                }
                else if (((current is XmlSchemaInclude) && (schemaLocation != null)) && (schemaLocation.Length > 0))
                {
                    var str2 = Path.GetFullPath(schemaLocation).ToLower();
                    if (includeSchemas[str2] == null)
                    {
                        var schema2 = ReadSchema(schemaLocation);
                        includeSchemas[str2] = schema2;
                        CollectIncludes(schema2, includeSchemas);
                    }
                    current.Schema = (XmlSchema) includeSchemas[str2];
                    current.SchemaLocation = null;
                }
            }
        }

        private void Compile(XmlSchemas userSchemas)
        {
            var parent = new XmlSchema();
            foreach (XmlSchema schema2 in userSchemas)
            {
                if ((schema2.TargetNamespace != null) && (schema2.TargetNamespace.Length == 0))
                {
                    schema2.TargetNamespace = null;
                }
                if (schema2.TargetNamespace == parent.TargetNamespace)
                {
                    var item = new XmlSchemaInclude();
                    item.Schema = schema2;
                    parent.Includes.Add(item);
                }
                else
                {
                    var import = new XmlSchemaImport();
                    import.Namespace = schema2.TargetNamespace;
                    import.Schema = schema2;
                    parent.Includes.Add(import);
                }
            }
            AddFakeSchemas(parent, userSchemas);
            try
            {
                var schemas = new XmlSchemaCollection();
                schemas.ValidationEventHandler += new ValidationEventHandler(this.ValidationCallbackWithErrorCode);
                schemas.Add(parent);
                if (schemas.Count == 0)
                {
                    this.CheckPoint(MessageType.Warning, schemaValidationFailure);
                }
            }
            catch (Exception exception)
            {
                this.CheckPoint(MessageType.Warning, schemaValidationFailure + "\n" + exception.Message);
            }
        }

        private void CreateCodeGenerator(out ICodeGenerator codeGen, out string fileExtension)
        {
            var language = this.WsdlProperties.Language;
            switch (language)
            {
                case Language.CS:
                    this.codeProvider = new CSharpCodeProvider();
                    break;

                case Language.VB:
                    this.codeProvider = new VBCodeProvider();
                    break;

                default:
                {
                    if (language != Language.Custom)
                    {
                        throw new Exception("Unknown language");
                    }
                    var type = Type.GetType(this.WsdlProperties.CustomCodeDomProvider);
                    if (type == null)
                    {
                        throw new TypeLoadException("Type '" + this.WsdlProperties.CustomCodeDomProvider + "' is not found");
                    }
                    this.codeProvider = (CodeDomProvider) Activator.CreateInstance(type);
                    break;
                }
            }
            if (this.codeProvider != null)
            {
                codeGen = this.codeProvider.CreateGenerator();
                fileExtension = this.codeProvider.FileExtension;
                if (fileExtension == null)
                {
                    fileExtension = string.Empty;
                }
                else if ((fileExtension.Length > 0) && (fileExtension[0] != '.'))
                {
                    fileExtension = "." + fileExtension;
                }
            }
            else
            {
                fileExtension = ".src";
                codeGen = null;
            }
        }

        static bool MyCertHandler(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors error)
        {
            // Ignore errors
            return true;
        }

        private DiscoveryClientProtocol CreateDiscoveryClient()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            var protocol = new DiscoveryClientProtocol();
            protocol.AllowAutoRedirect = true;
            
            if ((this.WsdlProperties.UserName != null) && (this.WsdlProperties.UserName.Length != 0))
            {
                protocol.Credentials = new NetworkCredential(this.WsdlProperties.UserName, this.WsdlProperties.Password, this.WsdlProperties.Domain);
            }
            else
            {
                protocol.Credentials = CredentialCache.DefaultCredentials;
            }
            if ((this.WsdlProperties.ProxyServer != null) && (this.WsdlProperties.ProxyServer.Length != 0))
            {
                IWebProxy proxy = null;
                proxy = new WebProxy(this.WsdlProperties.ProxyServer);
                proxy.Credentials = new NetworkCredential(this.WsdlProperties.ProxyUserName, this.WsdlProperties.ProxyPassword, this.WsdlProperties.ProxyDomain);
                protocol.Proxy = proxy;
            }
            return protocol;
        }

        private static XmlSchema CreateFakeSoapEncodingSchema(string ns, string name)
        {
            var schema = new XmlSchema();
            schema.TargetNamespace = ns;
            var item = new XmlSchemaGroup();
            item.Name = "Array";
            var sequence = new XmlSchemaSequence();
            var any = new XmlSchemaAny();
            any.MinOccurs = 0M;
            any.MaxOccurs = 79228162514264337593543950335M;
            sequence.Items.Add(any);
            any.Namespace = "##any";
            any.ProcessContents = XmlSchemaContentProcessing.Lax;
            item.Particle = sequence;
            schema.Items.Add(item);
            var type = new XmlSchemaComplexType();
            type.Name = name;
            var ref2 = new XmlSchemaGroupRef();
            ref2.RefName = new XmlQualifiedName("Array", ns);
            type.Particle = ref2;
            var attribute = new XmlSchemaAttribute();
            attribute.RefName = new XmlQualifiedName("arrayType", ns);
            type.Attributes.Add(attribute);
            schema.Items.Add(type);
            attribute = new XmlSchemaAttribute();
            attribute.Use = XmlSchemaUse.None;
            attribute.Name = "arrayType";
            schema.Items.Add(attribute);
            AddSimpleType(schema, "base64", "base64Binary");
            AddElementAndType(schema, "anyURI", ns);
            AddElementAndType(schema, "base64Binary", ns);
            AddElementAndType(schema, "boolean", ns);
            AddElementAndType(schema, "byte", ns);
            AddElementAndType(schema, "date", ns);
            AddElementAndType(schema, "dateTime", ns);
            AddElementAndType(schema, "decimal", ns);
            AddElementAndType(schema, "double", ns);
            AddElementAndType(schema, "duration", ns);
            AddElementAndType(schema, "ENTITIES", ns);
            AddElementAndType(schema, "ENTITY", ns);
            AddElementAndType(schema, "float", ns);
            AddElementAndType(schema, "gDay", ns);
            AddElementAndType(schema, "gMonth", ns);
            AddElementAndType(schema, "gMonthDay", ns);
            AddElementAndType(schema, "gYear", ns);
            AddElementAndType(schema, "gYearMonth", ns);
            AddElementAndType(schema, "hexBinary", ns);
            AddElementAndType(schema, "ID", ns);
            AddElementAndType(schema, "IDREF", ns);
            AddElementAndType(schema, "IDREFS", ns);
            AddElementAndType(schema, "int", ns);
            AddElementAndType(schema, "integer", ns);
            AddElementAndType(schema, "language", ns);
            AddElementAndType(schema, "long", ns);
            AddElementAndType(schema, "Name", ns);
            AddElementAndType(schema, "NCName", ns);
            AddElementAndType(schema, "negativeInteger", ns);
            AddElementAndType(schema, "NMTOKEN", ns);
            AddElementAndType(schema, "NMTOKENS", ns);
            AddElementAndType(schema, "nonNegativeInteger", ns);
            AddElementAndType(schema, "nonPositiveInteger", ns);
            AddElementAndType(schema, "normalizedString", ns);
            AddElementAndType(schema, "positiveInteger", ns);
            AddElementAndType(schema, "QName", ns);
            AddElementAndType(schema, "short", ns);
            AddElementAndType(schema, "string", ns);
            AddElementAndType(schema, "time", ns);
            AddElementAndType(schema, "token", ns);
            AddElementAndType(schema, "unsignedByte", ns);
            AddElementAndType(schema, "unsignedInt", ns);
            AddElementAndType(schema, "unsignedLong", ns);
            AddElementAndType(schema, "unsignedShort", ns);
            return schema;
        }

        private static XmlSchema CreateFakeWsdlSchema(string ns)
        {
            var schema = new XmlSchema();
            schema.TargetNamespace = ns;
            var item = new XmlSchemaAttribute();
            item.Use = XmlSchemaUse.None;
            item.Name = "arrayType";
            item.SchemaTypeName = new XmlQualifiedName("QName", "http://www.w3.org/2001/XMLSchema");
            schema.Items.Add(item);
            return schema;
        }

        private static XmlSchema CreateFakeXsdSchema(string ns, string name)
        {
            var schema = new XmlSchema();
            schema.TargetNamespace = ns;
            var item = new XmlSchemaElement();
            item.Name = name;
            var type = new XmlSchemaComplexType();
            item.SchemaType = type;
            schema.Items.Add(item);
            return schema;
        }

        private static bool FileExists(string path)
        {
            var flag = false;
            if ((path == null) || (path.Length == 0))
            {
                return false;
            }
            try
            {
                flag = (path.LastIndexOf('?') == -1) && System.IO.File.Exists(path);
            }
            catch
            {
            }
            return flag;
        }

        public void Generate()
        {
            this.CheckPoint(MessageType.Begin, "Initializing");
            var descriptions = new ServiceDescriptionCollection();
            var schemas = new XmlSchemas();
            var urls = new StringCollection();
            var localPaths = new StringCollection();
            this.GetPaths(localPaths, urls);
            descriptions.Clear();
            schemas.Clear();
            if ((localPaths != null) && (localPaths.Count > 0))
            {
                var path = localPaths[0];
                var extension = Path.GetExtension(path);
                if ((string.Compare(extension, ".exe", true) == 0) || (string.Compare(extension, ".dll", true) == 0))
                {
                    this.CheckPoint(MessageType.Begin, "Loading Assembly");
                    this.proxyAssembly = Assembly.LoadFrom(path);
                    if (this.proxyAssembly != null)
                    {
                        this.CheckPoint(MessageType.Success, "Loaded Assembly");
                    }
                    else
                    {
                        this.CheckPoint(MessageType.Failure, "Failed to load Assembly");
                    }
                    return;
                }
            }
            this.CheckPoint(MessageType.Begin, "Generating WSDL");
            try
            {
                var client = this.CreateDiscoveryClient();
                this.ProcessLocalPaths(client, localPaths, schemas, descriptions);
                this.ProcessRemoteUrls(client, urls, schemas, descriptions);
            }
            catch (Exception exception)
            {
                this.CheckPoint(MessageType.Failure, exception.ToString());
                return;
            }
            try
            {
                ICodeGenerator generator;
                string str3;
                this.CheckPoint(MessageType.Begin, "Generating Proxy");
                this.CreateCodeGenerator(out generator, out str3);
                var userSchemas = new XmlSchemas();
                userSchemas.Add(schemas);
                foreach (ServiceDescription description in descriptions)
                {
                    userSchemas.Add(description.Types.Schemas);
                }
                var includeSchemas = new Hashtable();
                foreach (XmlSchema schema in userSchemas)
                {
                    CollectIncludes(schema, includeSchemas);
                }
                this.Compile(userSchemas);
                this.GenerateCode(descriptions, schemas, "http://tempuri.org", generator, str3);
                this.CheckPoint(MessageType.Begin, "Compiling Proxy");
                this.GenerateAssembly();
                this.CheckPoint(MessageType.Success, "Generated Assembly");
            }
            catch (Exception exception2)
            {
                this.CheckPoint(MessageType.Failure, exception2.ToString());
            }
        }

        private void GenerateAssembly()
        {
            var compiler = this.codeProvider.CreateCompiler();
            var location = "";
            if ((this.WsdlProperties.ProxyBaseType != null) && (this.WsdlProperties.ProxyBaseType.Length > 0))
            {
                location = Type.GetType(this.WsdlProperties.ProxyBaseType, true).Assembly.Location;
            }
            var assemblyNames = new string[] { "System.Xml.dll", "System.dll", "System.Web.Services.dll", "System.Data.dll", Assembly.GetExecutingAssembly().Location, location };
            var options = new CompilerParameters(assemblyNames);
            options.WarningLevel = 0;
            options.GenerateInMemory = false;
            var results = compiler.CompileAssemblyFromSource(options, this.proxyCode);
            if (results.Errors.HasErrors)
            {
                foreach (CompilerError error in results.Errors)
                {
                    this.CheckPoint(MessageType.Error, error.ToString());
                }
                throw new Exception("CompilationErrors");
            }
            this.proxyAssembly = results.CompiledAssembly;
        }

        private void GenerateCode(ServiceDescriptionCollection sources, XmlSchemas schemas, string uriToWSDL, ICodeGenerator codeGen, string fileExtension)
        {
            this.proxyCode = " <ERROR> ";
            StringWriter w = null;
            this.compileUnit = new CodeCompileUnit();
            var importer = new ServiceDescriptionImporter();
            importer.Schemas.Add(schemas);
            foreach (ServiceDescription description in sources)
            {
                importer.AddServiceDescription(description, "", "");
            }
            importer.Style = ServiceDescriptionImportStyle.Client;
            var protocol = this.WsdlProperties.Protocol;
            importer.ProtocolName = this.WsdlProperties.Protocol.ToString();
            var namespace2 = new CodeNamespace(this.proxyNamespace);
            this.compileUnit.Namespaces.Add(namespace2);
            var warnings = importer.Import(namespace2, this.compileUnit);
            try
            {
                try
                {
                    w = new StringWriter();
                }
                catch
                {
                    throw;
                }
                MemoryStream stream = null;
                if (schemas.Count > 0)
                {
                    this.compileUnit.ReferencedAssemblies.Add("System.Data.dll");
                    foreach (XmlSchema schema in schemas)
                    {
                        string targetNamespace = null;
                        try
                        {
                            targetNamespace = schema.TargetNamespace;
                            if (XmlSchemas.IsDataSet(schema))
                            {
                                if (stream == null)
                                {
                                    stream = new MemoryStream();
                                }
                                stream.Position = 0L;
                                stream.SetLength(0L);
                                schema.Write(stream);
                                stream.Position = 0L;
                                var dataSet = new DataSet();
                                dataSet.ReadXmlSchema(stream);
                                TypedDataSetGenerator.Generate(dataSet, namespace2, codeGen);
                            }
                            continue;
                        }
                        catch
                        {
                            throw;
                        }
                    }
                }
                try
                {
                    GenerateVersionComment(this.compileUnit.Namespaces[0]);
                    this.ChangeBaseType(this.compileUnit);
                    codeGen.GenerateCodeFromCompileUnit(this.compileUnit, w, null);
                }
                catch (Exception exception)
                {
                    if (w != null)
                    {
                        w.Write("Exception in generating code");
                        w.Write(exception.Message);
                    }
                    throw new InvalidOperationException("Error generating ", exception);
                }
            }
            finally
            {
                this.proxyCode = w.ToString();
                if (w != null)
                {
                    w.Close();
                }
            }
        }

        private static void GenerateVersionComment(CodeNamespace codeNamespace)
        {
            codeNamespace.Comments.Add(new CodeCommentStatement(""));
            var name = Assembly.GetExecutingAssembly().GetName();
            var version = Environment.Version;
            codeNamespace.Comments.Add(new CodeCommentStatement("Assembly " + name.Name + " Version = " + version.ToString()));
            codeNamespace.Comments.Add(new CodeCommentStatement(""));
        }

        public ICodeGenerator GetCodeGenerator()
        {
            ICodeGenerator generator;
            string str;
            this.CreateCodeGenerator(out generator, out str);
            return generator;
        }

        private void GetPaths(StringCollection localPaths, StringCollection urls)
        {
            var enumerator = this.paths.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                if (FileExists(current))
                {
                    if (!Path.HasExtension(current))
                    {
                        throw new InvalidOperationException(current + " has no extensions");
                    }
                    localPaths.Add(current);
                }
                else
                {
                    Uri uri = null;
                    try
                    {
                        uri = new Uri(current, true);
                    }
                    catch (Exception)
                    {
                        uri = null;
                    }
                    if (uri == null)
                    {
                        throw new InvalidOperationException(current + " is invalid URI");
                    }
                    urls.Add(uri.AbsoluteUri);
                }
            }
        }

        private void ProcessLocalPaths(DiscoveryClientProtocol client, StringCollection localPaths, XmlSchemas schemas, ServiceDescriptionCollection descriptions)
        {
            var enumerator = localPaths.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                var extension = Path.GetExtension(current);
                if (string.Compare(extension, ".discomap", true) == 0)
                {
                    client.ReadAll(current);
                }
                else
                {
                    object document = null;
                    if (string.Compare(extension, ".wsdl", true) == 0)
                    {
                        document = this.ReadLocalDocument(false, current);
                    }
                    else
                    {
                        if (string.Compare(extension, ".xsd", true) != 0)
                        {
                            throw new InvalidOperationException("Unknown file type " + current);
                        }
                        document = this.ReadLocalDocument(true, current);
                    }
                    if (document != null)
                    {
                        this.AddDocument(current, document, schemas, descriptions);
                    }
                }
            }
        }

        private void ProcessRemoteUrls(DiscoveryClientProtocol client, StringCollection urls, XmlSchemas schemas, ServiceDescriptionCollection descriptions)
        {
            var enumerator = urls.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                try
                {
                    var document = client.DiscoverAny(current);
                    client.ResolveAll();
                    continue;
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException("General Error " + current, exception);
                }
            }
            var enumerator2 = client.Documents.GetEnumerator();
            while (enumerator2.MoveNext())
            {
                var entry = (DictionaryEntry) enumerator2.Current;
                this.AddDocument((string) entry.Key, entry.Value, schemas, descriptions);
            }
        }

        private object ReadLocalDocument(bool isSchema, string path)
        {
            object obj2 = null;
            StreamReader input = null;
            try
            {
                input = new StreamReader(path);
                if (isSchema)
                {
                    return XmlSchema.Read(new XmlTextReader(input), null);
                }
                obj2 = ServiceDescription.Read(input.BaseStream);
            }
            catch
            {
                throw;
            }
            finally
            {
                if (input != null)
                {
                    input.Close();
                }
            }
            return obj2;
        }

        public static XmlSchema ReadSchema(string filename)
        {
            var reader = new XmlTextReader(filename);
            if (reader.IsStartElement("schema", "http://www.w3.org/2001/XMLSchema"))
            {
                return XmlSchema.Read(reader, null);
            }
            return null;
        }

        public void Reset()
        {
            this.paths.Clear();
            this.codeProvider = null;
            this.wsdls.Clear();
            this.xsds.Clear();
            this.compileUnit = null;
            this.proxyAssembly = null;
            this.proxyCode = null;
            this.cancelled = false;
        }

        private void ValidationCallbackWithErrorCode(object sender, ValidationEventArgs args)
        {
            this.CheckPoint(MessageType.Warning, "Schema parsing error " + args.Message);
        }

        public StringCollection Paths
        {
            get
            {
                return this.paths;
            }
        }

        public Assembly ProxyAssembly
        {
            get
            {
                return this.proxyAssembly;
            }
        }

        public string ProxyCode
        {
            get
            {
                return this.proxyCode;
            }
        }

        public CodeCompileUnit ProxyCodeDom
        {
            get
            {
                return this.compileUnit;
            }
        }

        public string ProxyFileExtension
        {
            get
            {
                return this.WsdlProperties.Language.ToString();
            }
        }

        public string ProxyNamespace
        {
            get
            {
                return this.proxyNamespace;
            }
            set
            {
                this.proxyNamespace = value;
            }
        }

        public WebServiceStudio.WsdlProperties WsdlProperties
        {
            get
            {
                return Configuration.MasterConfig.WsdlSettings;
            }
        }

        public StringCollection Wsdls
        {
            get
            {
                return this.wsdls;
            }
        }

        public StringCollection Xsds
        {
            get
            {
                return this.xsds;
            }
        }

        internal class Namespace
        {
            public const string SoapEncoding = "http://schemas.xmlsoap.org/soap/encoding/";
            public const string Wsdl = "http://schemas.xmlsoap.org/wsdl/";
        }
    }
}

