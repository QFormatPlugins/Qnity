using System;
using System.Collections.Generic;
using UnityEngine;

namespace Qnity
{
    [Serializable]
    public class PointEntity
    {
        public string className;
        public GameObject prefab;
        
        public void SetupPrefab(GameObject go, Dictionary<string,string> attributes,Vector3 origin, float angle ,float inverseScale)
        {
            var parser = go.GetComponent<EntityPropertyReceiver>();
            var receiver = go.GetComponent<EntityEventReceiver>();
            var emitter = go.GetComponent<EntityEventEmitter>();

            if (parser != null) parser.inverseScale = inverseScale;
            
            go.transform.position = new Vector3(-origin.y, origin.z, origin.x) / inverseScale;
            go.transform.Rotate(Vector3.up, angle+90, Space.Self);
            
            foreach (var prop in attributes)
            {
                switch (prop.Key)
                {
                    case "target" when emitter != null:
                        emitter.SetTarget(prop.Value);
                        break;
                    case "targetname" when receiver != null:
                        receiver.targetName = prop.Value;
                        break;
                }

                if (prop.Key != "classname" && parser != null) parser.OnProperty(prop.Key, prop.Value);
            }
        }
    }
}
