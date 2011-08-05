﻿#region Copyright(c) Alexey Sadomov, Vladimir Timashkov. All Rights Reserved.
// -----------------------------------------------------------------------------
// Copyright(c) 2010 Alexey Sadomov, Vladimir Timashkov. All Rights Reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
//   1. No Trademark License - Microsoft Public License (Ms-PL) does not grant you rights to use
//      authors names, logos, or trademarks.
//   2. If you distribute any portion of the software, you must retain all copyright,
//      patent, trademark, and attribution notices that are present in the software.
//   3. If you distribute any portion of the software in source code form, you may do
//      so only under this license by including a complete copy of Microsoft Public License (Ms-PL)
//      with your distribution. If you distribute any portion of the software in compiled
//      or object code form, you may only do so under a license that complies with
//      Microsoft Public License (Ms-PL).
//   4. The names of the authors may not be used to endorse or promote products
//      derived from this software without specific prior written permission.
//
// The software is licensed "as-is." You bear the risk of using it. The authors
// give no express warranties, guarantees or conditions. You may have additional consumer
// rights under your local laws which this license cannot change. To the extent permitted
// under your local laws, the authors exclude the implied warranties of merchantability,
// fitness for a particular purpose and non-infringement.
// -----------------------------------------------------------------------------
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using CamlexNET.Impl.Helpers;
using CamlexNET.Impl.Operands;
using CamlexNET.Interfaces;

namespace CamlexNET.Impl.Factories
{
    internal class OperandBuilder : IOperandBuilder
    {
        // ----- Field Ref Operand -----

        public IOperand CreateFieldRefOperand(Expression expr, IOperand valueOperand)
        {
            if (expr is UnaryExpression)
            {
                expr = ((UnaryExpression)expr).Operand;
            }
            var methodCallExpression = expr as MethodCallExpression;
            var argumentExpression = methodCallExpression.Arguments[0];
            if (argumentExpression is ConstantExpression)
            {
                return this.createFieldRefOperandFromConstantExpression(argumentExpression as ConstantExpression,
                    valueOperand);
            }
            return this.createFieldRefOperandFromNonConstantExpression(argumentExpression, valueOperand);
        }

        private IOperand createFieldRefOperandFromNonConstantExpression(Expression expr, IOperand valueOperand)
        {
            object value = this.evaluateExpression(expr);
            if (value == null || (value.GetType() != typeof(string) && value.GetType() != typeof(Guid)))
            {
                throw new InvalidValueForFieldRefException(value);
            }

            if (value.GetType() == typeof(Guid))
            {
                return this.createFieldRefOperand((Guid)value, valueOperand);
            }
            return this.createFieldRefOperandByNameOrId((string)value, valueOperand);
        }

        private IOperand createFieldRefOperandFromConstantExpression(ConstantExpression expr, IOperand valueOperand)
        {
            var val = expr.Value as string;
            return this.createFieldRefOperandByNameOrId(val, valueOperand);
        }

        private IOperand createFieldRefOperandByNameOrId(string val, IOperand valueOperand)
        {
            // if string represents guid, then FieldRef with ID should be created
            try
            {
                var guid = new Guid(val);
                return this.createFieldRefOperand(guid, valueOperand);
            }
            catch
            {
                return this.createFieldRefOperand(val, valueOperand);
            }
        }

        private IOperand createFieldRefOperand(string fieldName, IOperand valueOperand)
        {
            var attrs = this.getAdditionalAttributesForFieldRefOperands(valueOperand);
            return new FieldRefOperand(fieldName, attrs);
        }

        private List<KeyValuePair<string, string>> getAdditionalAttributesForFieldRefOperands(IOperand valueOperand)
        {
            if (valueOperand is LookupIdValueOperand)
            {
                var attrs = new List<KeyValuePair<string, string>>();
                attrs.Add(new KeyValuePair<string, string>(Attributes.LookupId, true.ToString()));
                return attrs;
            }
            return new List<KeyValuePair<string, string>>();
        }

        private IOperand createFieldRefOperand(Guid id, IOperand valueOperand)
        {
            var attrs = this.getAdditionalAttributesForFieldRefOperands(valueOperand);
            return new FieldRefOperand(id, attrs);
        }

        public IOperand CreateFieldRefOperandWithOrdering(Expression expr, Camlex.OrderDirection orderDirection)
        {
            var fieldRefOperand = (FieldRefOperand)CreateFieldRefOperand(expr, null);
            return new FieldRefOperandWithOrdering(fieldRefOperand, orderDirection);
        }

        // ----- Value Operand -----

        public IOperand CreateValueOperandForNativeSyntax(Expression expr)
        {
            // determine operand type from expression result (specify null as explicitOperandType)
            return CreateValueOperandForNativeSyntax(expr, null);
        }

        public IOperand CreateValueOperandForNativeSyntax(Expression expr, Type explicitOperandType)
        {
            return CreateValueOperandForNativeSyntax(expr, explicitOperandType, expr);
        }

        private IOperand CreateValueOperandForNativeSyntax(Expression expr, Type explicitOperandType, Expression sourceExpr)
        {
            if (expr is ConstantExpression)
            {
                return this.createValueOperandFromConstantExpression(expr as ConstantExpression, explicitOperandType, sourceExpr);
            }
            return this.createValueOperandFromNonConstantExpression(expr, explicitOperandType, sourceExpr);
        }

        public IOperand CreateValueOperandForStringBasedSyntax(Expression expr)
        {
            var newExpr = ExpressionsHelper.RemoveIncludeTimeValueMethodCallIfAny(expr);

            // retrieve internal native expression from string based syntax
            var internalExpression = ((UnaryExpression)((UnaryExpression)newExpr).Operand).Operand;
            // use conversion type as operand type (subclass of BaseFieldType should be used here)
            // because conversion operand has always string type for string based syntax
            return this.CreateValueOperandForNativeSyntax(internalExpression, newExpr.Type, expr);
        }

        private IOperand createValueOperandFromNonConstantExpression(Expression expr, Type explicitOperandType, Expression sourceExpr)
        {
            object value = this.evaluateExpression(expr);

            // if operand type is not specified explicitly try to determine operand type from expression result
            var operandType = explicitOperandType;
            if (operandType == null)
            {
                // value can be null
                operandType = value != null ? value.GetType() : null;
            }
            return this.createValueOperand(operandType, value, sourceExpr);
        }

        private object evaluateExpression(Expression expr)
        {
            // need to add Expression.Convert(..) in order to define Func<object>
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object)));
            return lambda.Compile().Invoke();
        }

        private IOperand createValueOperandFromConstantExpression(ConstantExpression expr, Type explicitOperandType, Expression sourceExpr)
        {
            // if operand type is not specified explicitly try to determine operand type from expression type
            var operandType = explicitOperandType;
            if (operandType == null)
            {
                operandType = expr.Type;
            }
            return this.createValueOperand(operandType, expr.Value, sourceExpr);
        }

        private IOperand createValueOperand(Type type, object value, Expression expr)
        {
            // it is important to have check on NullValueOperand on 1st place
            if (value == null)
            {
                return new NullValueOperand();
            }
            // string operand can be native or string based
            if (type == typeof(string) || type == typeof(DataTypes.Text))
            {
                return new TextValueOperand((string)value);
            }
            // integer operand can be native or string based
            if (type == typeof(int) || type == typeof(DataTypes.Integer))
            {
                if (value.GetType() == typeof(int))
                {
                    return new IntegerValueOperand((int) value);
                }
                if (value.GetType() == typeof(string))
                {
                    return new IntegerValueOperand((string)value);
                }
            }
            // boolean operand can be native or string based
            if (type == typeof(bool) || type == typeof(DataTypes.Boolean))
            {
                if (value.GetType() == typeof(bool))
                {
                    return new BooleanValueOperand((bool)value);
                }
                if (value.GetType() == typeof(string))
                {
                    return new BooleanValueOperand((string)value);
                }
            }
            // DateTime operand can be native or string based
            if (type == typeof(DateTime) || type == typeof(DataTypes.DateTime))
            {
                var includeTimeValue = ExpressionsHelper.IncludeTimeValue(expr);

                if (value.GetType() == typeof(DateTime))
                {
                    return new DateTimeValueOperand((DateTime)value, includeTimeValue);
                }
                if (value.GetType() == typeof(string))
                {
                    return new DateTimeValueOperand((string)value, includeTimeValue);
                }
            }
            // guid operand can be native or string based
            if (type == typeof(Guid) || type == typeof(DataTypes.Guid))
            {
                if (value.GetType() == typeof(Guid))
                {
                    return new GuidValueOperand((Guid)value);
                }
                if (value.GetType() == typeof(string))
                {
                    return new GuidValueOperand((string)value);
                }
            }
            // for rest of generic types create generic string based operand
            if (type.IsSubclassOf(typeof(BaseFieldType)))
            {
                if (type == typeof(DataTypes.LookupId))
                {
                    return new LookupIdValueOperand((string)value);
                }
                else if (type == typeof(DataTypes.LookupValue))
                {
                    return new LookupValueValueOperand((string)value);
                }
                else if (type == typeof(DataTypes.UserId))
                {
                    return new UserIdValueOperand((string)value);
                }
                return new GenericStringBasedValueOperand(type, (string) value);
            }
            throw new NonSupportedOperandTypeException(type);
        }
    }
}