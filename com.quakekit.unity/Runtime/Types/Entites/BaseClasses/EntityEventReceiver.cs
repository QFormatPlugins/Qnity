using UnityEngine;

namespace QuakeKit
{

    public class EntityEventReceiver : MonoBehaviour
    {
        public string targetName;
        public virtual void OnTrigger()
        {
            Debug.Log(targetName + " got triggered");
        }
    }
}