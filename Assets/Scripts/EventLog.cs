using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class EventLog : MonoBehaviour
{
    public TextMeshProUGUI Log;
    public int LineCount = 16;

    private SizedQueue<string> LogMessages;
    private StringBuilder LogText;

    // Awake, not Start: a message can arrive in the frame the parent activates, before Start would run.
    private void Awake()
    {
        LogMessages = new SizedQueue<string>(LineCount);
        LogText = new StringBuilder();
        Log.text = LogText.ToString();
    }

    public void LogMessage(string text)
    {
        LogMessage(text, Color.white);
    }

    public void LogMessage(string text, Color color)
    {
        LogMessages.Enqueue($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}");
        LogText.Clear();
        foreach (var s in LogMessages) LogText.AppendLine(s);
        Log.text = LogText.ToString();
    }
}
