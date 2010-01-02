﻿using System;
using System.Xml.Linq;
using Camlex.NET.Interfaces;

namespace Camlex.NET.Impl.Operands
{
    public class TextValueOperand : ValueOperand<string>
    {
        public TextValueOperand(string value) :
            base(DataType.Text, value)
        {
        }

        public override XElement ToCaml()
        {
            return
                new XElement(Tags.Value, new XAttribute(Attributes.Type, this.type),
                    new XText(this.value));
        }
    }
}


