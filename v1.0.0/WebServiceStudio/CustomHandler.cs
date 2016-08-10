namespace WebServiceStudio
{
    using System;
    using System.ComponentModel;
    using System.Xml.Serialization;

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class CustomHandler
    {
        private string handler;
        private string typeName;

        public override string ToString()
        {
            return (this.handler + "{" + this.typeName + "}");
        }

        [XmlAttribute]
        public string Handler
        {
            get
            {
                return this.handler;
            }
            set
            {
                this.handler = value;
            }
        }

        [XmlAttribute]
        public string TypeName
        {
            get
            {
                return this.typeName;
            }
            set
            {
                this.typeName = value;
            }
        }
    }
}

