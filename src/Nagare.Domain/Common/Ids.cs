namespace Nagare.Domain.Common;

public readonly record struct ProfileId(Guid Value)
{
    public static ProfileId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct ChannelId(Guid Value)
{
    public static ChannelId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
