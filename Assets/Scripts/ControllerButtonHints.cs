using TMPro;
using UnityEngine;

public class ControllerButtonHints : MonoBehaviour
{
	[SerializeField]
	Transform rightController;

	[SerializeField]
	Transform lookTarget;

	[SerializeField]
	bool buildWhenMissing = true;

	[SerializeField]
	bool billboardToView = true;

	[SerializeField]
	Vector3 aLocalPosition = new Vector3(0.035f, 0.018f, 0.025f);

	[SerializeField]
	Vector3 bLocalPosition = new Vector3(0.012f, 0.045f, 0.025f);

	[SerializeField]
	Vector3 labelLocalEulerAngles = new Vector3(65f, 0f, 0f);

	[SerializeField, Min(0.01f)]
	float labelFontSize = 0.11f;

	TextMeshPro aLabel;

	TextMeshPro bLabel;

	public void Initialize (Transform viewTransform)
	{
		if (lookTarget == null)
		{
			lookTarget = viewTransform;
		}

		EnsureLabels();
	}

	void LateUpdate ()
	{
		EnsureLabels();
		if (aLabel == null || bLabel == null)
		{
			return;
		}

		UpdateLabelTransform(aLabel.transform, aLocalPosition);
		UpdateLabelTransform(bLabel.transform, bLocalPosition);
	}

	void EnsureLabels ()
	{
		if (rightController == null)
		{
			rightController = FindRightController();
		}

		if (rightController == null || !buildWhenMissing)
		{
			return;
		}

		if (aLabel == null)
		{
			aLabel = CreateLabel("A Button Hint", "A", aLocalPosition);
		}

		if (bLabel == null)
		{
			bLabel = CreateLabel("B Button Hint", "B", bLocalPosition);
		}
	}

	TextMeshPro CreateLabel (string name, string text, Vector3 localPosition)
	{
		var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
		labelObject.transform.SetParent(rightController, false);

		var label = labelObject.GetComponent<TextMeshPro>();
		label.text = text;
		label.fontSize = labelFontSize;
		label.alignment = TextAlignmentOptions.Center;
		label.color = Color.white;
		label.fontStyle = FontStyles.Bold;
		label.outlineWidth = 0.25f;
		label.outlineColor = Color.black;
		label.raycastTarget = false;
		label.rectTransform.sizeDelta = new Vector2(0.18f, 0.12f);

		UpdateLabelTransform(labelObject.transform, localPosition);
		return label;
	}

	void UpdateLabelTransform (Transform labelTransform, Vector3 localPosition)
	{
		labelTransform.localPosition = localPosition;
		if (billboardToView && lookTarget != null)
		{
			Vector3 direction = labelTransform.position - lookTarget.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
			{
				labelTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			}
		}
		else
		{
			labelTransform.localRotation = Quaternion.Euler(labelLocalEulerAngles);
		}
	}

	Transform FindRightController ()
	{
		GameObject controllerObject = GameObject.Find("Right Controller");
		if (controllerObject != null)
		{
			return controllerObject.transform;
		}

		Transform[] transforms = FindObjectsByType<Transform>(
			FindObjectsInactive.Include,
			FindObjectsSortMode.None
		);
		for (int i = 0; i < transforms.Length; i++)
		{
			if (transforms[i].name.Contains("Right Controller"))
			{
				return transforms[i];
			}
		}
		return null;
	}
}
