using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUiController : MonoBehaviour
{
	[SerializeField]
	bool buildDefaultUiWhenMissing = true;

	[SerializeField]
	GameObject mainMenuPanel;

	[SerializeField]
	GameObject calibrationPanel;

	[SerializeField]
	GameObject hudPanel;

	[SerializeField]
	GameObject pausePanel;

	[SerializeField]
	GameObject endPanel;

	[SerializeField]
	TMP_Text calibrationTitleText;

	[SerializeField]
	TMP_Text calibrationBodyText;

	[SerializeField]
	TMP_Text calibrationActionsText;

	[SerializeField]
	TMP_Text hudModeText;

	[SerializeField]
	TMP_Text hudActionText;

	[SerializeField]
	TMP_Text pauseModeText;

	[SerializeField]
	TMP_Text endMessageText;

	[SerializeField]
	VoiceMeterView calibrationMeter;

	[SerializeField]
	VoiceMeterView hudVoiceMeter;

	bool isInitialized;

	public void Initialize (Transform viewTransform)
	{
		if (!isInitialized && mainMenuPanel == null && buildDefaultUiWhenMissing)
		{
			BuildDefaultUi(viewTransform);
		}

		HideAll();
		isInitialized = true;
	}

	public void ShowMainMenu ()
	{
		HideAll();
		SetPanel(mainMenuPanel, true);
	}

	public void ShowCalibrationQuiet ()
	{
		ShowCalibration(
			"Voice calibration",
			"Step 1 of 2\nStay quiet for 2 seconds so the maze learns your resting volume.",
			"A: Record quiet\nB: Back"
		);
	}

	public void ShowCalibrationLoud (float quietVolume)
	{
		ShowCalibration(
			"Voice calibration",
			"Step 2 of 2\nSpeak loudly for 2 seconds. Louder voice means faster movement.\nQuiet baseline: " +
				quietVolume.ToString("0.000"),
			"A: Record loud\nB: Recalibrate"
		);
	}

	public void ShowCalibrationRecording (string title, string body)
	{
		ShowCalibration(
			title,
			body + "\nRecording...",
			"Hold still until this screen advances."
		);
	}

	public void ShowCalibrationFallback (
		float volume,
		float quietVolume,
		float loudVolume,
		string microphoneStatus
	)
	{
		ShowCalibration(
			"Voice range too small",
			"Quiet and loud were too close together.\nYou can start anyway with a fallback range, or recalibrate for better control.",
			"A: Start anyway\nB: Recalibrate"
		);
		UpdateCalibrationMeter(volume, quietVolume, loudVolume, microphoneStatus, "Fallback range");
	}

	public void ShowMicrophoneError (string microphoneStatus)
	{
		ShowCalibration(
			"Microphone unavailable",
			"Check headset permission and microphone input, then try voice mode again.",
			"A: Controller\nB: Try voice again"
		);
		UpdateCalibrationMeter(0f, 0f, 0.1f, microphoneStatus, "Mic not ready");
	}

	public void UpdateCalibrationMeter (
		float volume,
		float quietVolume,
		float loudVolume,
		string microphoneStatus,
		string label
	)
	{
		if (calibrationMeter != null)
		{
			calibrationMeter.SetVisible(true);
			calibrationMeter.UpdateMeter(
				volume, quietVolume, loudVolume, microphoneStatus, label, false
			);
		}
	}

	public void ShowHud (MovementModality modality)
	{
		HideAll();
		SetPanel(hudPanel, true);
		if (hudModeText != null)
		{
			hudModeText.text = modality == MovementModality.Voice ? "Voice" : "Controller";
		}
		if (hudActionText != null)
		{
			hudActionText.text = "B: Menu";
		}
		if (hudVoiceMeter != null)
		{
			hudVoiceMeter.SetDisplayOptions(false, false, false);
			hudVoiceMeter.SetVisible(modality == MovementModality.Voice);
		}
	}

	public void UpdateHudVoiceMeter (
		float volume,
		float quietVolume,
		float loudVolume,
		string microphoneStatus
	)
	{
		if (hudVoiceMeter != null)
		{
			hudVoiceMeter.UpdateMeter(
				volume, quietVolume, loudVolume, microphoneStatus, null
			);
		}
	}

	public void ShowPause (MovementModality modality)
	{
		HideAll();
		SetPanel(hudPanel, true);
		SetPanel(pausePanel, true);
		if (pauseModeText != null)
		{
			pauseModeText.text = modality == MovementModality.Voice ?
				"Voice maze paused" : "Controller maze paused";
		}
	}

	public void ShowEnd (string message)
	{
		HideAll();
		SetPanel(endPanel, true);
		if (endMessageText != null)
		{
			endMessageText.text = message;
		}
	}

	void ShowCalibration (string title, string body, string actions)
	{
		HideAll();
		SetPanel(calibrationPanel, true);
		if (calibrationTitleText != null)
		{
			calibrationTitleText.text = title;
		}
		if (calibrationBodyText != null)
		{
			calibrationBodyText.text = body;
		}
		if (calibrationActionsText != null)
		{
			calibrationActionsText.text = actions;
		}
		if (calibrationMeter != null)
		{
			calibrationMeter.SetDisplayOptions(true, true, true);
			calibrationMeter.SetVisible(true);
		}
	}

	void HideAll ()
	{
		SetPanel(mainMenuPanel, false);
		SetPanel(calibrationPanel, false);
		SetPanel(hudPanel, false);
		SetPanel(pausePanel, false);
		SetPanel(endPanel, false);
	}

	void SetPanel (GameObject panel, bool visible)
	{
		if (panel != null)
		{
			panel.SetActive(visible);
		}
	}

	void BuildDefaultUi (Transform viewTransform)
	{
		Transform parent = viewTransform != null ? viewTransform : transform;
		var canvasObject = new GameObject(
			"Demo UI Canvas",
			typeof(RectTransform),
			typeof(Canvas),
			typeof(CanvasScaler),
			typeof(GraphicRaycaster)
		);
		canvasObject.transform.SetParent(parent, false);
		canvasObject.transform.localPosition = new Vector3(0f, -0.05f, 1.6f);
		canvasObject.transform.localRotation = Quaternion.identity;
		canvasObject.transform.localScale = Vector3.one * 0.0014f;

		var canvas = canvasObject.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.WorldSpace;
		canvas.worldCamera = parent.GetComponent<Camera>();

		var canvasRect = canvasObject.GetComponent<RectTransform>();
		canvasRect.sizeDelta = new Vector2(1000f, 620f);

		var scaler = canvasObject.GetComponent<CanvasScaler>();
		scaler.dynamicPixelsPerUnit = 8f;

		mainMenuPanel = CreatePanel(canvasRect, "MainMenuPanel", new Color(0.02f, 0.03f, 0.04f, 0.84f));
		CreateText(mainMenuPanel.transform, "Title", "MAGIC MIC", 64, FontStyles.Bold, new Vector2(0f, 190f), new Vector2(850f, 90f));
		CreateText(
			mainMenuPanel.transform,
			"Subtitle",
			"Choose how you want to navigate the maze.",
			28,
			FontStyles.Normal,
			new Vector2(0f, 125f),
			new Vector2(850f, 60f)
		);
		CreateOption(mainMenuPanel.transform, "A: Controller", new Vector2(0f, 20f));
		CreateOption(mainMenuPanel.transform, "B: Voice", new Vector2(0f, -85f));
		CreateText(
			mainMenuPanel.transform,
			"Footer",
			"Voice mode uses microphone volume to move forward.",
			22,
			FontStyles.Normal,
			new Vector2(0f, -215f),
			new Vector2(850f, 50f)
		);

		calibrationPanel = CreatePanel(canvasRect, "CalibrationPanel", new Color(0.02f, 0.03f, 0.04f, 0.86f));
		calibrationTitleText = CreateText(calibrationPanel.transform, "Title", "", 44, FontStyles.Bold, new Vector2(0f, 205f), new Vector2(850f, 70f));
		calibrationBodyText = CreateText(calibrationPanel.transform, "Body", "", 28, FontStyles.Normal, new Vector2(0f, 105f), new Vector2(820f, 130f));
		calibrationMeter = CreateMeter(calibrationPanel.transform, "CalibrationMeter", new Vector2(0f, -45f), 760f);
		calibrationActionsText = CreateText(calibrationPanel.transform, "Actions", "", 26, FontStyles.Bold, new Vector2(0f, -215f), new Vector2(850f, 80f));

		hudPanel = CreateHudPanel(canvasRect);
		hudModeText = CreateText(hudPanel.transform, "Mode", "Controller", 14, FontStyles.Normal, new Vector2(-405f, -270f), new Vector2(180f, 28f), TextAlignmentOptions.Left);
		hudModeText.color = new Color(1f, 1f, 1f, 0.55f);
		hudActionText = CreateText(hudPanel.transform, "Action", "B: Menu", 14, FontStyles.Normal, new Vector2(405f, -270f), new Vector2(180f, 28f), TextAlignmentOptions.Right);
		hudActionText.color = new Color(1f, 1f, 1f, 0.55f);
		hudVoiceMeter = CreateCompactMeter(hudPanel.transform, "HudVoiceMeter", new Vector2(0f, -270f), 260f);

		pausePanel = CreatePanel(canvasRect, "PausePanel", new Color(0.01f, 0.015f, 0.02f, 0.92f));
		CreateText(pausePanel.transform, "Title", "Paused", 58, FontStyles.Bold, new Vector2(0f, 105f), new Vector2(850f, 90f));
		pauseModeText = CreateText(pausePanel.transform, "Mode", "", 28, FontStyles.Normal, new Vector2(0f, 25f), new Vector2(850f, 60f));
		CreateOption(pausePanel.transform, "A: Resume", new Vector2(0f, -65f));
		CreateOption(pausePanel.transform, "B: Leave Maze", new Vector2(0f, -170f));

		endPanel = CreatePanel(canvasRect, "EndPanel", new Color(0.02f, 0.015f, 0.015f, 0.9f));
		CreateText(endPanel.transform, "Title", "Run complete", 38, FontStyles.Bold, new Vector2(0f, 150f), new Vector2(850f, 70f));
		endMessageText = CreateText(endPanel.transform, "Message", "", 56, FontStyles.Bold, new Vector2(0f, 45f), new Vector2(850f, 115f));
		CreateOption(endPanel.transform, "A: Main Menu", new Vector2(0f, -135f));
	}

	GameObject CreatePanel (
		Transform parent,
		string name,
		Color backgroundColor
	)
	{
		var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		panel.transform.SetParent(parent, false);
		var rect = panel.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(45f, 45f);
		rect.offsetMax = new Vector2(-45f, -45f);
		var image = panel.GetComponent<Image>();
		image.color = backgroundColor;
		return panel;
	}

	GameObject CreateHudPanel (Transform parent)
	{
		var panel = new GameObject("HudPanel", typeof(RectTransform));
		panel.transform.SetParent(parent, false);
		var rect = panel.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		return panel;
	}

	TMP_Text CreateText (
		Transform parent,
		string name,
		string text,
		float fontSize,
		FontStyles style,
		Vector2 position,
		Vector2 size,
		TextAlignmentOptions alignment = TextAlignmentOptions.Center
	)
	{
		var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
		textObject.transform.SetParent(parent, false);
		var rect = textObject.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = size;

		var tmp = textObject.GetComponent<TextMeshProUGUI>();
		tmp.text = text;
		tmp.fontSize = fontSize;
		tmp.fontStyle = style;
		tmp.alignment = alignment;
		tmp.color = Color.white;
		tmp.textWrappingMode = TextWrappingModes.Normal;
		tmp.raycastTarget = false;
		return tmp;
	}

	void CreateOption (Transform parent, string text, Vector2 position)
	{
		var option = new GameObject("Option", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		option.transform.SetParent(parent, false);
		var rect = option.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = new Vector2(720f, 76f);
		option.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
		CreateText(option.transform, "Text", text, 30, FontStyles.Bold, Vector2.zero, new Vector2(650f, 60f));
	}

	VoiceMeterView CreateMeter (Transform parent, string name, Vector2 position, float width)
	{
		var root = new GameObject(name, typeof(RectTransform));
		root.transform.SetParent(parent, false);
		var rootRect = root.GetComponent<RectTransform>();
		rootRect.anchorMin = new Vector2(0.5f, 0.5f);
		rootRect.anchorMax = new Vector2(0.5f, 0.5f);
		rootRect.anchoredPosition = position;
		rootRect.sizeDelta = new Vector2(width, 120f);

		var label = CreateText(root.transform, "Label", "Quiet / idle", 24, FontStyles.Bold, new Vector2(0f, 43f), new Vector2(width, 34f));

		var track = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		track.transform.SetParent(root.transform, false);
		var trackRect = track.GetComponent<RectTransform>();
		trackRect.anchorMin = new Vector2(0.5f, 0.5f);
		trackRect.anchorMax = new Vector2(0.5f, 0.5f);
		trackRect.anchoredPosition = new Vector2(0f, 8f);
		trackRect.sizeDelta = new Vector2(width, 26f);
		track.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);

		var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		fill.transform.SetParent(track.transform, false);
		var fillRect = fill.GetComponent<RectTransform>();
		fillRect.anchorMin = Vector2.zero;
		fillRect.anchorMax = Vector2.one;
		fillRect.offsetMin = Vector2.zero;
		fillRect.offsetMax = Vector2.zero;
		var fillImage = fill.GetComponent<Image>();
		fillImage.type = Image.Type.Filled;
		fillImage.fillMethod = Image.FillMethod.Horizontal;
		fillImage.fillAmount = 0f;
		fillImage.color = new Color(0.35f, 0.85f, 1f);

		var value = CreateText(root.transform, "Value", "Volume 0.000", 18, FontStyles.Normal, new Vector2(0f, -26f), new Vector2(width, 28f));
		var status = CreateText(root.transform, "Status", "MIC OFF", 14, FontStyles.Normal, new Vector2(0f, -54f), new Vector2(width, 24f));

		var meter = root.AddComponent<VoiceMeterView>();
		meter.Configure(root, fillImage, label, value, status);
		return meter;
	}

	VoiceMeterView CreateCompactMeter (
		Transform parent,
		string name,
		Vector2 position,
		float width
	)
	{
		var root = new GameObject(name, typeof(RectTransform));
		root.transform.SetParent(parent, false);
		var rootRect = root.GetComponent<RectTransform>();
		rootRect.anchorMin = new Vector2(0.5f, 0.5f);
		rootRect.anchorMax = new Vector2(0.5f, 0.5f);
		rootRect.anchoredPosition = position;
		rootRect.sizeDelta = new Vector2(width, 18f);

		var track = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		track.transform.SetParent(root.transform, false);
		var trackRect = track.GetComponent<RectTransform>();
		trackRect.anchorMin = new Vector2(0.5f, 0.5f);
		trackRect.anchorMax = new Vector2(0.5f, 0.5f);
		trackRect.anchoredPosition = Vector2.zero;
		trackRect.sizeDelta = new Vector2(width, 8f);
		track.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

		var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		fill.transform.SetParent(track.transform, false);
		var fillRect = fill.GetComponent<RectTransform>();
		fillRect.anchorMin = Vector2.zero;
		fillRect.anchorMax = Vector2.one;
		fillRect.offsetMin = Vector2.zero;
		fillRect.offsetMax = Vector2.zero;
		var fillImage = fill.GetComponent<Image>();
		fillImage.type = Image.Type.Filled;
		fillImage.fillMethod = Image.FillMethod.Horizontal;
		fillImage.fillAmount = 0f;
		fillImage.color = new Color(0.35f, 0.85f, 1f, 0.7f);

		var meter = root.AddComponent<VoiceMeterView>();
		meter.Configure(root, fillImage, null, null, null);
		meter.SetDisplayOptions(false, false, false);
		return meter;
	}
}
