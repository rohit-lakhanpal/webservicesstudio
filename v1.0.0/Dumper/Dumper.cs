namespace Dumper
{
    using System;
    using System.Collections;
    using System.Data;
    using System.IO;
    using System.Reflection;
    using System.Xml;

    public class Dumper
    {
        private int indent;
        private Hashtable objects;
        private TextWriter writer;

        public Dumper() : this(Console.Out)
        {
        }

        public Dumper(TextWriter writer)
        {
            this.objects = new Hashtable();
            this.indent = 0;
            this.writer = writer;
        }

        public static void Dump(string name, object o)
        {
            new Dumper().DumpInternal(name, o);
        }

        private void DumpInternal(string name, object o)
        {
            for (var i = 0; i < this.indent; i++)
            {
                this.writer.Write("--- ");
            }
            if (name == null)
            {
                name = string.Empty;
            }
            if (o == null)
            {
                this.writer.WriteLine(name + " = null");
            }
            else
            {
                var c = o.GetType();
                this.writer.Write(c.Name + " " + name);
                if (this.objects[o] != null)
                {
                    this.writer.WriteLine(" = ...");
                }
                else
                {
                    if (!c.IsValueType && (c != typeof(string)))
                    {
                        this.objects.Add(o, o);
                    }
                    if (c.IsArray)
                    {
                        var array = (Array) o;
                        this.writer.WriteLine();
                        this.indent++;
                        for (var j = 0; j < array.Length; j++)
                        {
                            this.DumpInternal("[" + j + "]", array.GetValue(j));
                        }
                        this.indent--;
                    }
                    else if (o is XmlQualifiedName)
                    {
                        this.DumpInternal("Name", ((XmlQualifiedName) o).Name);
                        this.DumpInternal("Namespace", ((XmlQualifiedName) o).Namespace);
                    }
                    else if (o is XmlNode)
                    {
                        this.writer.WriteLine(" = " + ((XmlNode) o).OuterXml.Replace('\n', ' ').Replace('\r', ' '));
                    }
                    else if (c.IsEnum)
                    {
                        this.writer.WriteLine(" = " + ((Enum) o).ToString());
                    }
                    else if (c.IsPrimitive)
                    {
                        this.writer.WriteLine(" = " + o.ToString());
                    }
                    else if (typeof(Exception).IsAssignableFrom(c))
                    {
                        this.writer.WriteLine(" = " + ((Exception) o).Message);
                    }
                    else if (o is DataSet)
                    {
                        this.writer.WriteLine();
                        this.indent++;
                        this.DumpInternal("Tables", ((DataSet) o).Tables);
                        this.indent--;
                    }
                    else if (o is DateTime)
                    {
                        this.writer.WriteLine(" = " + o.ToString());
                    }
                    else if (o is DataTable)
                    {
                        this.writer.WriteLine();
                        this.indent++;
                        var table = (DataTable) o;
                        this.DumpInternal("TableName", table.TableName);
                        this.DumpInternal("Rows", table.Rows);
                        this.indent--;
                    }
                    else if (o is DataRow)
                    {
                        this.writer.WriteLine();
                        this.indent++;
                        var row = (DataRow) o;
                        this.DumpInternal("Values", row.ItemArray);
                        this.indent--;
                    }
                    else if (o is string)
                    {
                        var str2 = (string) o;
                        if (str2.Length > 40)
                        {
                            this.writer.WriteLine(" = ");
                            this.writer.WriteLine("\"" + str2 + "\"");
                        }
                        else
                        {
                            this.writer.WriteLine(" = \"" + str2 + "\"");
                        }
                    }
                    else if (o is IEnumerable)
                    {
                        var enumerator = ((IEnumerable) o).GetEnumerator();
                        if (enumerator == null)
                        {
                            this.writer.WriteLine(" GetEnumerator() == null");
                        }
                        else
                        {
                            this.writer.WriteLine();
                            var num3 = 0;
                            this.indent++;
                            while (enumerator.MoveNext())
                            {
                                this.DumpInternal("[" + num3 + "]", enumerator.Current);
                                num3++;
                            }
                            this.indent--;
                        }
                    }
                    else
                    {
                        this.writer.WriteLine();
                        this.indent++;
                        if (typeof(Type).IsAssignableFrom(c) || typeof(PropertyInfo).IsAssignableFrom(c))
                        {
                            foreach (var info in c.GetProperties())
                            {
                                if ((info.CanRead && (typeof(IEnumerable).IsAssignableFrom(info.PropertyType) || info.CanWrite)) && (info.PropertyType != c))
                                {
                                    object obj2;
                                    try
                                    {
                                        obj2 = info.GetValue(o, null);
                                    }
                                    catch (Exception exception)
                                    {
                                        obj2 = exception;
                                    }
                                    this.DumpInternal(info.Name, obj2);
                                }
                            }
                        }
                        else
                        {
                            foreach (var info2 in c.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                            {
                                if (!info2.IsStatic)
                                {
                                    this.DumpInternal(info2.Name, info2.GetValue(o));
                                }
                            }
                        }
                        this.indent--;
                    }
                }
            }
        }
    }
}

