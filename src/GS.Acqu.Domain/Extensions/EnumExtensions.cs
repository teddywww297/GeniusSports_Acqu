using System.ComponentModel;
using System.Reflection;

namespace GS.Acqu.Domain.Extensions;

/// <summary>
/// 列舉擴充方法
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// 取得列舉的 Description 屬性值
    /// </summary>
    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        
        if (field == null) return value.ToString();

        var attribute = field.GetCustomAttribute<DescriptionAttribute>();
        
        return attribute?.Description ?? value.ToString();
    }

    /// <summary>
    /// 從 Description 取得列舉值
    /// </summary>
    public static T? GetEnumFromDescription<T>(string description) where T : struct, Enum
    {
        foreach (var field in typeof(T).GetFields())
        {
            var attribute = field.GetCustomAttribute<DescriptionAttribute>();
            
            if (attribute?.Description == description)
            {
                return (T?)field.GetValue(null);
            }
            
            if (field.Name == description)
            {
                return (T?)field.GetValue(null);
            }
        }

        return null;
    }

    /// <summary>
    /// 取得列舉的所有值及其描述
    /// </summary>
    public static IEnumerable<(T Value, string Description)> GetAllWithDescriptions<T>() where T : struct, Enum
    {
        return Enum.GetValues<T>().Select(e => (e, e.GetDescription()));
    }
}

