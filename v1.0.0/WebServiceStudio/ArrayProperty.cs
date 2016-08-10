namespace WebServiceStudio
{
    using System;
    using System.ComponentModel;
    using System.Windows.Forms;

    internal class ArrayProperty : ClassProperty
    {
        public ArrayProperty(System.Type[] possibleTypes, string name, Array val) : base(possibleTypes, name, val)
        {
        }

        protected override void CreateChildren()
        {
            base.TreeNode.Nodes.Clear();
            if (this.OkayToCreateChildren())
            {
                var elementType = this.Type.GetElementType();
                var length = this.Length;
                for (var i = 0; i < length; i++)
                {
                    var val = this.ArrayValue.GetValue(i);
                    if ((val == null) && this.IsInput())
                    {
                        val = TreeNodeProperty.CreateNewInstance(elementType);
                    }
                    TreeNodeProperty.CreateTreeNodeProperty(base.GetIncludedTypes(elementType), base.Name + "_" + i.ToString(), val).RecreateSubtree(base.TreeNode);
                }
            }
        }

        public override object ReadChildren()
        {
            var arrayValue = this.ArrayValue;
            if (arrayValue == null)
            {
                return null;
            }
            var num = 0;
            for (var i = 0; i < arrayValue.Length; i++)
            {
                var node = base.TreeNode.Nodes[num++];
                var tag = node.Tag as TreeNodeProperty;
                if (tag != null)
                {
                    arrayValue.SetValue(tag.ReadChildren(), i);
                }
            }
            return arrayValue;
        }

        private Array ArrayValue
        {
            get
            {
                return (base.InternalValue as Array);
            }
            set
            {
                base.InternalValue = value;
            }
        }

        [RefreshProperties(RefreshProperties.All)]
        public virtual int Length
        {
            get
            {
                return ((this.ArrayValue != null) ? this.ArrayValue.Length : 0);
            }
            set
            {
                var length = this.Length;
                var num2 = value;
                var destinationArray = Array.CreateInstance(this.Type.GetElementType(), num2);
                if (this.ArrayValue != null)
                {
                    Array.Copy(this.ArrayValue, destinationArray, Math.Min(num2, length));
                }
                this.ArrayValue = destinationArray;
                base.TreeNode.Text = this.ToString();
                this.CreateChildren();
            }
        }
    }
}

