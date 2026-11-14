public interface ISpawnerHoldable
{
    void OnSpawnerHoldStart(); // pause self-destroy, etc.
    void OnSpawnerHoldEnd();   // resume
}
