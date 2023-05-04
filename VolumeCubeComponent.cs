using UnityEngine;

public class VolumeCubeComponent : MonoBehaviour
{
    public bool IsTouchingSomething;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<SmokeGrenadeScript>() != null)
            return;

        IsTouchingSomething = true;
    }
  
}
