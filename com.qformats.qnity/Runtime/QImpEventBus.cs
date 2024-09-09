using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class QnityEventEntry
{
    public string name;
    public UnityEvent unityEvent = new UnityEvent();

    public QnityEventEntry(string targetName)
    {
        name = targetName;
    }
}

public class QnityEventBus : MonoBehaviour
{
    public List<QnityEventEntry> eventList = new List<QnityEventEntry>();


    public void FireEvent(string targetName)
    {
        var ev = FindEvent(targetName);
        if (ev != null)
        {
            ev.Invoke();
        }
    }

    public UnityEvent FindEvent(string targetName)
    {
        foreach (var ev in eventList)
        {
            if (ev.name == targetName) return ev.unityEvent;
        }
        return null;
    }
}
