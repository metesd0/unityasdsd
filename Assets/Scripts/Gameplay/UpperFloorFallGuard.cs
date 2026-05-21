using UnityEngine;

namespace MobilOfl.Gameplay
{
    public class UpperFloorFallGuard : MonoBehaviour
    {
        [SerializeField] private Vector3 boundsCenter = new Vector3(918f, 8.05f, -566f);
        [SerializeField] private Vector3 boundsSize = new Vector3(42f, 3.2f, 122f);
        [SerializeField] private float upperGroundY = 8.18f;
        [SerializeField] private float armedY = 7.35f;
        [SerializeField] private float catchY = 7.15f;
        [SerializeField] private float snapCooldown = 0.2f;

        private CharacterController _playerController;
        private PrototypeFirstPersonController _movement;
        private bool _wasOnUpperFloor;
        private float _nextSnapAt;

        private void LateUpdate()
        {
            ResolvePlayer();
            if (_playerController == null)
            {
                return;
            }

            var player = _playerController.transform;
            var position = player.position;
            if (!IsInsideHorizontalBounds(position))
            {
                if (position.y < armedY)
                {
                    _wasOnUpperFloor = false;
                }

                return;
            }

            if (position.y >= armedY)
            {
                _wasOnUpperFloor = true;
            }

            if (!_wasOnUpperFloor || position.y > catchY || Time.time < _nextSnapAt)
            {
                return;
            }

            SnapToUpperFloor(player);
        }

        private void SnapToUpperFloor(Transform player)
        {
            _nextSnapAt = Time.time + snapCooldown;
            var wasEnabled = _playerController.enabled;
            _playerController.enabled = false;
            player.position = new Vector3(player.position.x, upperGroundY, player.position.z);
            _playerController.enabled = wasEnabled;
            _wasOnUpperFloor = true;

            if (_movement != null)
            {
                _movement.ResetVerticalVelocity();
            }
        }

        private bool IsInsideHorizontalBounds(Vector3 position)
        {
            var half = boundsSize * 0.5f;
            return position.x >= boundsCenter.x - half.x &&
                   position.x <= boundsCenter.x + half.x &&
                   position.z >= boundsCenter.z - half.z &&
                   position.z <= boundsCenter.z + half.z;
        }

        private void ResolvePlayer()
        {
            if (_playerController != null && _playerController.enabled && _playerController.gameObject.activeInHierarchy)
            {
                return;
            }

            var player = GameObject.Find("Player");
            if (player == null)
            {
                return;
            }

            _playerController = player.GetComponent<CharacterController>();
            _movement = player.GetComponent<PrototypeFirstPersonController>();
        }
    }
}
