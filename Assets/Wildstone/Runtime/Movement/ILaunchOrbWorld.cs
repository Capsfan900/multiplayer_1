namespace Wildstone.Movement
{
    public interface ILaunchOrbWorld
    {
        bool TryConsume(MotorLaunchOrb contact, out int action);
    }
}
