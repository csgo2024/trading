using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using Trading.Common.Extensions;

namespace Trading.Infrastructure.Conventions;

public class SnakeCaseElementNameConvention : IMemberMapConvention
{
    public string Name => "SnakeCaseElementNameConvention";

    public void Apply(BsonMemberMap memberMap)
    {
        memberMap.SetElementName(memberMap.MemberName.ToSnakeCase());
    }
}
