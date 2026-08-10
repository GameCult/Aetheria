/// <summary>
/// Marshals events and data between ConsoleController and uGUI.
/// </summary>
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Collections;
using System.Linq;
using TMPro;
using UniRx;
using UnityEngine.InputSystem;

public class ConsoleView : MonoBehaviour {
	bool _didShow = false;

	public ActionGameManager GameManager;
	public GameObject ViewContainer; //Container for console view, should be a child of this GameObject
	public TextMeshProUGUI LogTextArea;
	public TextMeshProUGUI InputArea;
    public ScrollRect Scroll;

    private string _inputString = "";
    private CursorLockMode _previousCursorLockMode;
    private bool _playerInputPreviouslyEnabled;

    public string InputString
    {
	    get => _inputString;
	    set
	    {
		    _inputString = value;
		    InputArea.text = $"> {_inputString}";
	    }
    }

    //public bool AcceptingInput { get; private set; }

    public bool Visible { get; private set; }

    void Start()
	{
		//AcceptingInput = false;
        //_console = new ConsoleController();
        Application.logMessageReceived += OnLogCallback;

        SetVisibility(false);

		ConsoleController.Instance.VisibilityChanged += OnVisibilityChanged;
        ConsoleController.Instance.LogChanged += OnLogChanged;
		
		Keyboard.current.onTextInput += c =>
		{
			if (!Visible || c == '`') return;
			InputString += c;
		};
		InputString = "";
		
		ConsoleController.AddCommand("echo", Echo);
		
		// var sizeFitter = GetComponentInChildren<ContentSizeFitter>().on
	}

    private void OnLogCallback(string condition, string trace, LogType type)
    {
	    ConsoleController.Instance.AppendLogLine("<color=" + (type == LogType.Error ? "red" : type == LogType.Warning ? "yellow" : "white") + ">" + condition + "</color>");
    }

    private void OnDestroy() {
	    Application.logMessageReceived -= OnLogCallback;
        ConsoleController.Instance.VisibilityChanged -= OnVisibilityChanged;
        ConsoleController.Instance.LogChanged -= OnLogChanged;
	}
	
	void Update()
	{
		var keyboard = Keyboard.current;
		//Toggle visibility when tilde key pressed
		if (keyboard.backquoteKey.wasPressedThisFrame) {
			ToggleVisibility();
		}

		if (!Visible) return;
		
		if(keyboard.deleteKey.wasPressedThisFrame && InputString.Length>0)
	    {
		    InputString = InputString.Substring(0, InputString.Length - 1);
	    }

	    if (keyboard.enterKey.wasPressedThisFrame)
	    {
		    if (/*AcceptingInput && */InputString.Length > 0)
		    {
			    //if(_inputString.StartsWith("!") || _inputString.StartsWith("/"))
				    ConsoleController.Instance.RunCommandString(InputString);//.Substring(1));
			    //else ConsoleController.Instance.RunCommandString($"say \"{_inputString}\"");
			    InputString = "";
		    }
		    //AcceptingInput = !AcceptingInput;
	    }
	}

    void Echo(string[] args)
    {
        if(args.Length>0)
            Debug.Log(args.Aggregate((a,b) => a + " " + b));
    }

	void ToggleVisibility() {
		SetVisibility(!ViewContainer.activeSelf);
	}
	
	void SetVisibility(bool visible) {
		ViewContainer.SetActive(visible);
	    Visible = visible;
		if(visible)
		{
			_playerInputPreviouslyEnabled = GameManager.Input.Player.enabled;
			_previousCursorLockMode = Cursor.lockState;
			GameManager.Input.Global.Disable();
			GameManager.DisablePlayerInput();
			GameManager.Input.UI.Disable();
			Cursor.lockState = CursorLockMode.None;
		}
		else
		{
			if(_playerInputPreviouslyEnabled)
				GameManager.EnablePlayerInput();
			else GameManager.Input.UI.Enable();
			GameManager.Input.Global.Enable();
			Cursor.lockState = _previousCursorLockMode;
		}
	}

    void OnVisibilityChanged(bool visible) {
		SetVisibility(visible);
	}
	
	void OnLogChanged(string[] newLog) {
		LogTextArea.text = string.Join("\n", newLog);
		// if (AcceptingInput)
		// 	LogTextArea.text += "\n\n> " + _inputString;
		Observable.NextFrame().Subscribe(_ =>
		{
			if(Scroll)
				Scroll.verticalNormalizedPosition = 0;
		});
	}

}
