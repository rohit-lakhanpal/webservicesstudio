namespace WebServiceStudio
{
    using System;
    using System.Collections;
    using System.Reflection;
    using System.Web.Services.Protocols;
    using System.Windows.Forms;

    internal class MethodProperty : TreeNodeProperty
    {
        private bool isIn;
        private MethodInfo method;
        private object[] paramValues;
        private ProxyProperty proxyProperty;
        private object result;

        public MethodProperty(ProxyProperty proxyProperty, MethodInfo method) : base(new System.Type[] { method.ReturnType }, method.Name)
        {
            this.proxyProperty = proxyProperty;
            this.method = method;
            this.isIn = true;
        }

        public MethodProperty(ProxyProperty proxyProperty, MethodInfo method, object result, object[] paramValues) : base(new System.Type[] { method.ReturnType }, method.Name)
        {
            this.proxyProperty = proxyProperty;
            this.method = method;
            this.isIn = false;
            this.result = result;
            this.paramValues = paramValues;
        }

        private void AddBody()
        {
            var parentNode = base.TreeNode.Nodes.Add("Body");
            if (!this.isIn && (this.method.ReturnType != typeof(void)))
            {
                var type = (this.result != null) ? this.result.GetType() : this.method.ReturnType;
                TreeNodeProperty.CreateTreeNodeProperty(new System.Type[] { type }, "result", this.result).RecreateSubtree(parentNode);
            }
            var parameters = this.method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                if ((!this.isIn && (parameters[i].IsOut || parameters[i].ParameterType.IsByRef)) || (this.isIn && !parameters[i].IsOut))
                {
                    var parameterType = parameters[i].ParameterType;
                    if (parameterType.IsByRef)
                    {
                        parameterType = parameterType.GetElementType();
                    }
                    var val = (this.paramValues != null) ? this.paramValues[i] : (this.isIn ? TreeNodeProperty.CreateNewInstance(parameterType) : null);
                    TreeNodeProperty.CreateTreeNodeProperty(base.GetIncludedTypes(parameterType), parameters[i].Name, val).RecreateSubtree(parentNode);
                }
            }
            parentNode.ExpandAll();
        }

        private void AddHeaders()
        {
            var parentNode = base.TreeNode.Nodes.Add("Headers");
            var soapHeaders = GetSoapHeaders(this.method, this.isIn);
            var proxy = this.proxyProperty.GetProxy();
            foreach (var info in soapHeaders)
            {
                var val = (proxy != null) ? info.GetValue(proxy) : null;
                TreeNodeProperty.CreateTreeNodeProperty(base.GetIncludedTypes(info.FieldType), info.Name, val).RecreateSubtree(parentNode);
            }
            parentNode.ExpandAll();
        }

        protected override void CreateChildren()
        {
            this.AddHeaders();
            this.AddBody();
        }

        protected override MethodInfo GetCurrentMethod()
        {
            return this.method;
        }

        protected override object GetCurrentProxy()
        {
            return this.proxyProperty.GetProxy();
        }

        public MethodInfo GetMethod()
        {
            return this.method;
        }

        public ProxyProperty GetProxyProperty()
        {
            return this.proxyProperty;
        }

        public static FieldInfo[] GetSoapHeaders(MethodInfo method, bool isIn)
        {
            var declaringType = method.DeclaringType;
            var customAttributes = (SoapHeaderAttribute[]) method.GetCustomAttributes(typeof(SoapHeaderAttribute), true);
            var list = new ArrayList();
            for (var i = 0; i < customAttributes.Length; i++)
            {
                var attribute = customAttributes[i];
                if (((attribute.Direction == SoapHeaderDirection.InOut) || (isIn && (attribute.Direction == SoapHeaderDirection.In))) || (!isIn && (attribute.Direction == SoapHeaderDirection.Out)))
                {
                    var field = declaringType.GetField(attribute.MemberName);
                    list.Add(field);
                }
            }
            return (FieldInfo[]) list.ToArray(typeof(FieldInfo));
        }

        protected override bool IsInput()
        {
            return this.isIn;
        }

        private void ReadBody()
        {
            var node = base.TreeNode.Nodes[1];
            var parameters = this.method.GetParameters();
            this.paramValues = new object[parameters.Length];
            var index = 0;
            var num2 = 0;
            while (index < this.paramValues.Length)
            {
                var info = parameters[index];
                if (!info.IsOut)
                {
                    var node2 = node.Nodes[num2++];
                    var tag = node2.Tag as TreeNodeProperty;
                    if (tag != null)
                    {
                        this.paramValues[index] = tag.ReadChildren();
                    }
                }
                index++;
            }
        }

        public override object ReadChildren()
        {
            this.ReadHeaders();
            this.ReadBody();
            return this.paramValues;
        }

        private void ReadHeaders()
        {
            var node = base.TreeNode.Nodes[0];
            var declaringType = this.method.DeclaringType;
            var proxy = this.proxyProperty.GetProxy();
            foreach (TreeNode node2 in node.Nodes)
            {
                var tag = node2.Tag as ClassProperty;
                if (tag != null)
                {
                    declaringType.GetField(tag.Name).SetValue(proxy, tag.ReadChildren());
                }
            }
        }

        public override string ToString()
        {
            return base.Name;
        }
    }
}

