using UnityEngine;

namespace LeiHuo.Gameplay.TemperatureField
{
    [DisallowMultipleComponent]
    public class TemperatureFieldEnhancerPickup : MonoBehaviour
    {
        [Header("Pickup")]
        [SerializeField] private bool requireTrigger = true;
        [SerializeField] private bool consumeIfAlreadyEnhanced = true;
        [SerializeField] private bool destroyOnPickup = true;
        [SerializeField] private GameObject pickupEffectPrefab;
        [SerializeField] private bool logStateChanges;

        private bool hasBeenPickedUp;

        private void Reset()
        {
            Collider pickupCollider = GetComponent<Collider>();
            if (pickupCollider != null)
            {
                pickupCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPickup(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (requireTrigger)
            {
                return;
            }

            TryPickup(collision.collider);
        }

        private void TryPickup(Collider other)
        {
            if (hasBeenPickedUp || other == null)
            {
                return;
            }

            TemperatureFieldController controller = other.GetComponentInParent<TemperatureFieldController>();
            if (controller == null)
            {
                return;
            }

            bool granted = controller.TryGrantEnhancement();
            if (!granted && !consumeIfAlreadyEnhanced)
            {
                return;
            }

            hasBeenPickedUp = true;
            SpawnPickupEffect();

            if (logStateChanges)
            {
                string message = granted
                    ? $"{name} granted a temperature field enhancement to {controller.name}."
                    : $"{name} was collected by {controller.name}, but the enhancement was already stored.";
                Debug.Log(message, this);
            }

            if (destroyOnPickup)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void SpawnPickupEffect()
        {
            if (pickupEffectPrefab == null)
            {
                return;
            }

            Instantiate(pickupEffectPrefab, transform.position, transform.rotation);
        }
    }
}
