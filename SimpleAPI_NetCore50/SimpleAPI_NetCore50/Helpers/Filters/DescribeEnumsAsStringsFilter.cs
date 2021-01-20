using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;
using System.Reflection;

namespace SimpleAPI_NetCore50.Filters
{
    public class DescribeEnumsAsStringsFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.IsEnum)
            {
                schema.Enum.Clear();

                // get the enum description attribute or fallback to the property name
                foreach (MemberInfo member in context.Type.GetMembers(BindingFlags.Public | BindingFlags.Static))
                {
                    object[] noDocumentAttributes = member.GetCustomAttributes(typeof(Attributes.DoNotDocumentAttribute), false);
                    if(noDocumentAttributes.Length > 0)
                    {
                        continue;
                    }

                    string memberDescription = "[ERROR]";
                    object[] descriptionAttributes = member.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);

                    if(descriptionAttributes.Length > 0 && descriptionAttributes[0] != null)
                    {
                        memberDescription = ((System.ComponentModel.DescriptionAttribute)descriptionAttributes[0]).Description;
                    }
                    else
                    {
                        memberDescription = member.Name.ToLower();
                    }
                    schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(memberDescription));
                }
            }
        }
    }
}
