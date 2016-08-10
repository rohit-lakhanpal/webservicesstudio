namespace WebServiceStudio
{
    using System;
    using System.Collections;
    using System.Xml;

    internal class XmlElementProperty : ClassProperty
    {
        private static Type[] attrArrayType = new Type[] { typeof(XmlAttribute[]) };
        private static Type[] elemArrayType = new Type[] { typeof(XmlElement[]) };
        private static Type[] stringType = new Type[] { typeof(string) };

        public XmlElementProperty(Type[] possibleTypes, string name, object val) : base(possibleTypes, name, val)
        {
        }

        protected override void CreateChildren()
        {
            base.TreeNode.Nodes.Clear();
            if (base.InternalValue != null)
            {
                TreeNodeProperty.CreateTreeNodeProperty(stringType, "Name", this.xmlElement.Name).RecreateSubtree(base.TreeNode);
                TreeNodeProperty.CreateTreeNodeProperty(stringType, "NamespaceURI", this.xmlElement.NamespaceURI).RecreateSubtree(base.TreeNode);
                TreeNodeProperty.CreateTreeNodeProperty(stringType, "TextValue", this.xmlElement.InnerText).RecreateSubtree(base.TreeNode);
                var list = new ArrayList();
                var list2 = new ArrayList();
                if (this.xmlElement != null)
                {
                    for (var node = this.xmlElement.FirstChild; node != null; node = node.NextSibling)
                    {
                        if (node.NodeType == XmlNodeType.Element)
                        {
                            list2.Add(node);
                        }
                    }
                    foreach (XmlAttribute attribute in this.xmlElement.Attributes)
                    {
                        if ((attribute.Name != "xmlns") && !attribute.Name.StartsWith("xmlns:"))
                        {
                            list.Add(attribute);
                        }
                    }
                }
                var val = ((list.Count == 0) && !this.IsInput()) ? null : (list.ToArray(typeof(XmlAttribute)) as XmlAttribute[]);
                var elementArray = ((list2.Count == 0) && !this.IsInput()) ? null : (list2.ToArray(typeof(XmlElement)) as XmlElement[]);
                TreeNodeProperty.CreateTreeNodeProperty(attrArrayType, "Attributes", val).RecreateSubtree(base.TreeNode);
                TreeNodeProperty.CreateTreeNodeProperty(elemArrayType, "SubElements", elementArray).RecreateSubtree(base.TreeNode);
            }
        }

        public XmlDocument GetXmlDocument()
        {
            var parent = base.GetParent() as ArrayProperty;
            XmlElementProperty property2 = null;
            if (parent != null)
            {
                property2 = parent.GetParent() as XmlElementProperty;
            }
            if (property2 == null)
            {
                return this.xmlElement.OwnerDocument;
            }
            return property2.GetXmlDocument();
        }

        public override object ReadChildren()
        {
            XmlElement element3;
            if (base.InternalValue == null)
            {
                return null;
            }
            var qualifiedName = ((TreeNodeProperty) base.TreeNode.Nodes[0].Tag).ReadChildren().ToString();
            var namespaceURI = ((TreeNodeProperty) base.TreeNode.Nodes[1].Tag).ReadChildren().ToString();
            var str3 = ((TreeNodeProperty) base.TreeNode.Nodes[2].Tag).ReadChildren().ToString();
            var attributeArray = (XmlAttribute[]) ((TreeNodeProperty) base.TreeNode.Nodes[3].Tag).ReadChildren();
            var elementArray = (XmlElement[]) ((TreeNodeProperty) base.TreeNode.Nodes[4].Tag).ReadChildren();
            var element = this.GetXmlDocument().CreateElement(qualifiedName, namespaceURI);
            if (attributeArray != null)
            {
                foreach (var attribute in attributeArray)
                {
                    element.SetAttributeNode(attribute);
                }
            }
            element.InnerText = str3;
            if (elementArray != null)
            {
                foreach (var element2 in elementArray)
                {
                    element.AppendChild(element2);
                }
            }
            this.xmlElement = element3 = element;
            return element3;
        }

        private XmlElement xmlElement
        {
            get
            {
                return (base.InternalValue as XmlElement);
            }
            set
            {
                base.InternalValue = value;
            }
        }
    }
}

