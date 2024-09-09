using UnityEngine;


public class EntityEventEmitter : MonoBehaviour
{
    public string target;
    public bool once;
    protected bool triggered = false;

    [SerializeField] protected QnityEventBus localEventBus;

    public void SetTarget(string targetName)
    {
        target = targetName;
    }

    public void SetLocalEventBus(QnityEventBus eb)
    {
        localEventBus = eb;
    }

    protected virtual void TriggerEntered(Collider col)
    {
        if (target != "" && (!once || !triggered))
        {
            localEventBus.FireEvent(target);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TriggerEntered(other);
    }
}
