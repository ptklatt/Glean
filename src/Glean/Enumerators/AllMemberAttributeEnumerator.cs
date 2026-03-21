using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;
using Glean.Extensions;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator that yields custom attributes matching a given namespace and name
/// across a type and all its members (methods, fields, properties, events).
/// </summary>
public struct AllMemberAttributeEnumerator : IStructEnumerator<Contexts.CustomAttributeContext>
{
    private enum Phase : byte
    {
        TypeAttrs   = 0,
        MethodOuter = 1,
        MethodAttrs = 2,
        FieldOuter  = 3,
        FieldAttrs  = 4,
        PropOuter   = 5,
        PropAttrs   = 6,
        EventOuter  = 7,
        EventAttrs  = 8,
        Done        = 9,
    }

    private readonly MetadataReader _reader;
    private readonly TypeDefinition _typeDef;
    private readonly string _ns;
    private readonly string _name;

    private Phase _phase;
    private CustomAttributeContext _current;

    private CustomAttributeHandleCollection.Enumerator _typeAttrEnum;
    private MethodDefinitionHandleCollection.Enumerator _methodEnum;
    private CustomAttributeHandleCollection.Enumerator _methodAttrEnum;
    private FieldDefinitionHandleCollection.Enumerator _fieldEnum;
    private CustomAttributeHandleCollection.Enumerator _fieldAttrEnum;
    private PropertyDefinitionHandleCollection.Enumerator _propEnum;
    private CustomAttributeHandleCollection.Enumerator _propAttrEnum;
    private EventDefinitionHandleCollection.Enumerator _eventEnum;
    private CustomAttributeHandleCollection.Enumerator _eventAttrEnum;

    internal static AllMemberAttributeEnumerator Create(TypeContext typeContext, string ns, string name)
    {
        return new AllMemberAttributeEnumerator(typeContext, ns, name);
    }

    private AllMemberAttributeEnumerator(TypeContext typeContext, string ns, string name)
    {
        _reader         = typeContext.Reader;
        _typeDef        = typeContext.Definition;
        _ns             = ns;
        _name           = name;
        _phase          = Phase.TypeAttrs;
        _current        = default;
        _typeAttrEnum   = _typeDef.GetCustomAttributes().GetEnumerator();
        _methodEnum     = default;
        _methodAttrEnum = default;
        _fieldEnum      = default;
        _fieldAttrEnum  = default;
        _propEnum       = default;
        _propAttrEnum   = default;
        _eventEnum      = default;
        _eventAttrEnum  = default;
    }

    public CustomAttributeContext Current => _current;

    public bool MoveNext()
    {
        while (true)
        {
            switch (_phase)
            {
                case Phase.TypeAttrs:
                    while (_typeAttrEnum.MoveNext())
                    {
                        var h = _typeAttrEnum.Current;
                        if (_reader.GetCustomAttribute(h).IsAttributeType(_reader, _ns, _name))
                        {
                            _current = CustomAttributeContext.Create(_reader, h);
                            return true;
                        }
                    }
                    _methodEnum = _typeDef.GetMethods().GetEnumerator();
                    _phase = Phase.MethodOuter;
                    continue;

                case Phase.MethodOuter:
                    if (_methodEnum.MoveNext())
                    {
                        var methodHandle = _methodEnum.Current;
                        _methodAttrEnum = _reader.GetMethodDefinition(methodHandle).GetCustomAttributes().GetEnumerator();
                        _phase = Phase.MethodAttrs;
                        continue;
                    }
                    _fieldEnum = _typeDef.GetFields().GetEnumerator();
                    _phase = Phase.FieldOuter;
                    continue;

                case Phase.MethodAttrs:
                    while (_methodAttrEnum.MoveNext())
                    {
                        var h = _methodAttrEnum.Current;
                        if (_reader.GetCustomAttribute(h).IsAttributeType(_reader, _ns, _name))
                        {
                            _current = CustomAttributeContext.Create(_reader, h);
                            return true;
                        }
                    }
                    _phase = Phase.MethodOuter;
                    continue;

                case Phase.FieldOuter:
                    if (_fieldEnum.MoveNext())
                    {
                        var fieldHandle = _fieldEnum.Current;
                        _fieldAttrEnum = _reader.GetFieldDefinition(fieldHandle).GetCustomAttributes().GetEnumerator();
                        _phase = Phase.FieldAttrs;
                        continue;
                    }
                    _propEnum = _typeDef.GetProperties().GetEnumerator();
                    _phase = Phase.PropOuter;
                    continue;

                case Phase.FieldAttrs:
                    while (_fieldAttrEnum.MoveNext())
                    {
                        var h = _fieldAttrEnum.Current;
                        if (_reader.GetCustomAttribute(h).IsAttributeType(_reader, _ns, _name))
                        {
                            _current = CustomAttributeContext.Create(_reader, h);
                            return true;
                        }
                    }
                    _phase = Phase.FieldOuter;
                    continue;

                case Phase.PropOuter:
                    if (_propEnum.MoveNext())
                    {
                        var propHandle = _propEnum.Current;
                        _propAttrEnum = _reader.GetPropertyDefinition(propHandle).GetCustomAttributes().GetEnumerator();
                        _phase = Phase.PropAttrs;
                        continue;
                    }
                    _eventEnum = _typeDef.GetEvents().GetEnumerator();
                    _phase = Phase.EventOuter;
                    continue;

                case Phase.PropAttrs:
                    while (_propAttrEnum.MoveNext())
                    {
                        var h = _propAttrEnum.Current;
                        if (_reader.GetCustomAttribute(h).IsAttributeType(_reader, _ns, _name))
                        {
                            _current = CustomAttributeContext.Create(_reader, h);
                            return true;
                        }
                    }
                    _phase = Phase.PropOuter;
                    continue;

                case Phase.EventOuter:
                    if (_eventEnum.MoveNext())
                    {
                        var eventHandle = _eventEnum.Current;
                        _eventAttrEnum = _reader.GetEventDefinition(eventHandle).GetCustomAttributes().GetEnumerator();
                        _phase = Phase.EventAttrs;
                        continue;
                    }
                    _phase = Phase.Done;
                    continue;

                case Phase.EventAttrs:
                    while (_eventAttrEnum.MoveNext())
                    {
                        var h = _eventAttrEnum.Current;
                        if (_reader.GetCustomAttribute(h).IsAttributeType(_reader, _ns, _name))
                        {
                            _current = CustomAttributeContext.Create(_reader, h);
                            return true;
                        }
                    }
                    _phase = Phase.EventOuter;
                    continue;

                default: // Phase.Done
                    _current = default;
                    return false;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AllMemberAttributeEnumerator GetEnumerator() => this;

    public void Dispose()
    {
    }
}
