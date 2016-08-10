namespace WebServiceStudio
{
    using System;
    using System.Reflection;
    using System.Web.Services.Protocols;
    using System.Windows.Forms;

    internal class ProxyProperty : TreeNodeProperty
    {
        private HttpWebClientProtocol proxy;
        private ProxyProperties proxyProperties;

        public ProxyProperty(HttpWebClientProtocol proxy) : base(new System.Type[] { typeof(ProxyProperties) }, "Proxy")
        {
            this.proxy = proxy;
            this.proxyProperties = new ProxyProperties(proxy);
        }

        protected override void CreateChildren()
        {
            base.TreeNode.Nodes.Clear();
            foreach (var info in this.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var val = info.GetValue(this.proxyProperties, null);
                TreeNodeProperty.CreateTreeNodeProperty(base.GetIncludedTypes(info.PropertyType), info.Name, val).RecreateSubtree(base.TreeNode);
            }
        }

        public HttpWebClientProtocol GetProxy()
        {
            ((ProxyProperties) this.ReadChildren()).UpdateProxy(this.proxy);
            return this.proxy;
        }

        public override object ReadChildren()
        {
            object proxyProperties = this.proxyProperties;
            if (proxyProperties == null)
            {
                return null;
            }
            var num = 0;
            foreach (var info in this.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var node = base.TreeNode.Nodes[num++];
                var tag = node.Tag as TreeNodeProperty;
                if (tag != null)
                {
                    info.SetValue(proxyProperties, tag.ReadChildren(), null);
                }
            }
            return proxyProperties;
        }
    }
}

