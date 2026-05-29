namespace Content.Shared._Stories.Ordnance;

public interface IExplosionModifierEffect
{
    void ModifyExplosionStats(ref float exPower, ref float exFalloff, ref float fireIntensity, ref float fireDuration, ref float fireRadius, float qty, float level);
}
