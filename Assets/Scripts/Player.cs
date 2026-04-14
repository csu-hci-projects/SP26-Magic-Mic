using Unity.XR.CoreUtils;
using UnityEngine;

[RequireComponent(typeof(XROrigin))]
public class Player : MonoBehaviour
{
	[SerializeField]
	XROrigin xrOrigin;

	Transform head;

	void Awake ()
	{
		if (xrOrigin == null)
		{
			xrOrigin = GetComponent<XROrigin>();
		}

		if (xrOrigin != null && xrOrigin.Camera != null)
		{
			head = xrOrigin.Camera.transform;
		}
	}

	public void StartNewGame (Vector3 position)
	{
		if (xrOrigin == null)
		{
			xrOrigin = GetComponent<XROrigin>();
		}

		if (xrOrigin != null && xrOrigin.Camera != null)
		{
			head = xrOrigin.Camera.transform;
		}

		float yaw = Random.Range(0f, 360f);
		transform.rotation = Quaternion.Euler(0f, yaw, 0f);

		// Move only in X/Z so we do not fight headset height tracking.
		if (head != null)
		{
			Vector3 delta = new Vector3(
				position.x - head.position.x,
				0f,
				position.z - head.position.z
			);
			transform.position += delta;
		}
		else
		{
			transform.position = new Vector3(
				position.x,
				transform.position.y,
				position.z
			);
		}
	}

	public Vector3 Move ()
	{
		if (head == null && xrOrigin != null && xrOrigin.Camera != null)
		{
			head = xrOrigin.Camera.transform;
		}

		Transform t = head != null ? head : transform;

		// Maze logic only cares about X/Z.
		return new Vector3(t.position.x, 0f, t.position.z);
	}
}