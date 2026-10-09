// 协议literal的只读元数据提取；不执行类型初始化、不加载程序集，不从程序集版本猜wire版本。
using System.Reflection;
using System.Reflection.Metadata;

namespace ScpCv.Contracts.Presentations.Dependencies;

internal static class PresentationDependencyProtocolVersionReader
{
    /// <summary>:param metadata: PEReader已读取的程序集元数据。:returns: 唯一指定公开Int32 literal；缺失或形状不符时无法确认。</summary>
    internal static int? Read(MetadataReader metadata)
    {
        TypeDefinitionHandle policy = default;
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(type.Namespace) != "ScpCv.Presentation.Protocol" ||
                metadata.GetString(type.Name) != "PresentationRequestPolicy") continue;
            if (!policy.IsNil) return null;
            policy = handle;
        }
        if (policy.IsNil) return null;
        FieldDefinitionHandle version = default;
        foreach (var handle in metadata.GetTypeDefinition(policy).GetFields())
        {
            if (metadata.GetString(metadata.GetFieldDefinition(handle).Name) != "ProtocolVersion") continue;
            if (!version.IsNil) return null;
            version = handle;
        }
        if (version.IsNil) return null;
        var field = metadata.GetFieldDefinition(version);
        if ((field.Attributes & FieldAttributes.FieldAccessMask) != FieldAttributes.Public ||
            (field.Attributes & (FieldAttributes.Static | FieldAttributes.Literal)) != (FieldAttributes.Static | FieldAttributes.Literal) ||
            field.GetDefaultValue().IsNil) return null;
        var signature = metadata.GetBlobReader(field.Signature);
        if (signature.Length != 2 || signature.ReadByte() != 0x06 || signature.ReadByte() != 0x08) return null;
        var constant = metadata.GetConstant(field.GetDefaultValue());
        if (constant.TypeCode != ConstantTypeCode.Int32) return null;
        var value = metadata.GetBlobReader(constant.Value);
        return value.Length == sizeof(int) ? value.ReadInt32() : null;
    }
}
