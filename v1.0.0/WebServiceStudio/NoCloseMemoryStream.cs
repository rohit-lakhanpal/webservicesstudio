namespace WebServiceStudio
{
    using System;
    using System.IO;

    internal class NoCloseMemoryStream : MemoryStream
    {
        public override void Close()
        {
        }
    }
}

