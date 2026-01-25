using System;
using System.Collections.Generic;
using UnityEngine;

namespace Qnity
{
    [Serializable]
    public class SolidEntity
    {
        public string className;
        public GameObject prefab;

        public static void SetupPrefab(GameObject go, Dictionary<string,string> attributes)
        {
            var parser = go.GetComponent<EntityPropertyReceiver>();
            var receiver = go.GetComponent<EntityEventReceiver>();
            var emitter = go.GetComponent<EntityEventEmitter>();
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

            return;
        }
    }
}
