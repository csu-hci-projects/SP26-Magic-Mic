using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VoiceMeterView : MonoBehaviour
{
	[SerializeField]
	GameObject root;

	[SerializeField]
	Image fillImage;

	[SerializeField]
	TMP_Text labelText;

	[SerializeField]
	TMP_Text valueText;

	[SerializeField]
	TMP_Text statusText;

	[SerializeField]
	bool showMovementLabel = true;

	[SerializeField]
	bool showValue = true;

	[SerializeField]
	bool showStatus = true;

	[SerializeField, Min(1f)]
	float rawVolumeScale = 50f;

	[SerializeField]
	Color quietColor = new Color(0.35f, 0.85f, 1f);

	[SerializeField]
	Color movingColor = new Color(0.4f, 1f, 0.55f);

	[SerializeField]
	Color loudColor = new Color(1f, 0.78f, 0.28f);

	public void SetDisplayOptions (
		bool showMovementLabel,
		bool showValue,
		bool showStatus
	)
	{
		this.showMovementLabel = showMovementLabel;
		this.showValue = showValue;
		this.showStatus = showStatus;
		UpdateTextVisibility();
	}

	public void Configure (
		GameObject rootObject,
		Image fill,
		TMP_Text label,
		TMP_Text value,
		TMP_Text status
	)
	{
		root = rootObject;
		fillImage = fill;
		labelText = label;
		valueText = value;
		statusText = status;
		UpdateTextVisibility();
	}

	public void SetVisible (bool visible)
	{
		GameObject target = root != null ? root : gameObject;
		target.SetActive(visible);
	}

	public void UpdateMeter (
		float volume,
		float quietVolume,
		float loudVolume,
		string microphoneStatus,
		string labelOverride = null,
		bool useCalibrationRange = true
	)
	{
		float normalized = GetNormalizedVolume(
			volume, quietVolume, loudVolume, useCalibrationRange
		);
		if (fillImage != null)
		{
			fillImage.fillAmount = normalized;
			fillImage.color = GetMeterColor(normalized);
		}

		string movementLabel = GetMovementLabel(normalized);
		if (labelText != null)
		{
			if (showMovementLabel)
			{
				labelText.text = string.IsNullOrEmpty(labelOverride) ?
					movementLabel : labelOverride + " - " + movementLabel;
			}
			else
			{
				labelText.text = labelOverride ?? "";
			}
		}

		if (valueText != null)
		{
			valueText.text =
				"Volume " + volume.ToString("0.000") +
				"   Quiet " + quietVolume.ToString("0.000") +
				"   Loud " + loudVolume.ToString("0.000");
		}

		if (statusText != null)
		{
			statusText.text = microphoneStatus;
		}
	}

	void UpdateTextVisibility ()
	{
		if (labelText != null)
		{
			labelText.gameObject.SetActive(showMovementLabel);
		}
		if (valueText != null)
		{
			valueText.gameObject.SetActive(showValue);
		}
		if (statusText != null)
		{
			statusText.gameObject.SetActive(showStatus);
		}
	}

	float GetNormalizedVolume (
		float volume,
		float quietVolume,
		float loudVolume,
		bool useCalibrationRange
	)
	{
		if (useCalibrationRange && loudVolume > quietVolume)
		{
			return Mathf.Clamp01(Mathf.InverseLerp(quietVolume, loudVolume, volume));
		}
		return Mathf.Clamp01(volume * rawVolumeScale);
	}

	Color GetMeterColor (float normalized)
	{
		if (normalized >= 0.75f)
		{
			return loudColor;
		}
		if (normalized >= 0.18f)
		{
			return movingColor;
		}
		return quietColor;
	}

	string GetMovementLabel (float normalized)
	{
		if (normalized >= 0.75f)
		{
			return "Loud / fast";
		}
		if (normalized >= 0.18f)
		{
			return "Moving";
		}
		return "Quiet / idle";
	}
}
