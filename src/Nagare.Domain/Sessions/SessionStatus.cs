namespace Nagare.Domain.Sessions;

public enum SessionStatus
{
    Starting,
    Running,
    Reconnecting,
    Stopped,   // terminal
    Failed     // terminal
}
