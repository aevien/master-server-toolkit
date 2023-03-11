#if MIRROR
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    [DisallowMultipleComponent]
    public class PlayerCharacterInput : MonoBehaviour
    {
        public virtual float Horizontal()
        {
            return Input.GetAxis("Horizontal");
        }

        public virtual float Vertical()
        {
            return Input.GetAxis("Vertical");
        }

        public virtual float MouseX()
        {
            return Input.GetAxis("Mouse X");
        }

        public virtual float MouseY()
        {
            return Input.GetAxis("Mouse Y");
        }

        public float MouseVerticalScroll()
        {
            return Input.mouseScrollDelta.y;
        }

        public virtual bool IsRotateCameraMode()
        {
            return Input.GetMouseButton(2);
        }

        public virtual Vector3 MovementAxisDirection()
        {
            return new Vector3(Horizontal(), 0.0f, Vertical()).normalized;
        }

        public virtual float MovementAxisMagnitude()
        {
            return MovementAxisDirection().magnitude;
        }

        public virtual bool IsMoving()
        {
            bool hasHorizontalInput = !Mathf.Approximately(Horizontal(), 0f);
            bool hasVerticalInput = !Mathf.Approximately(Vertical(), 0f);
            return hasHorizontalInput || hasVerticalInput;
        }

        public virtual bool IsArmed()
        {
            return Input.GetMouseButton(1);
        }

        public virtual bool IsAttack()
        {
            return Input.GetMouseButton(0);
        }

        public virtual bool IsCrouching()
        {
            return Input.GetKeyDown(KeyCode.C);
        }

        public virtual bool IsJump()
        {
            return Input.GetButton("Jump");
        }

        public virtual bool IsRunnning()
        {
            return Input.GetKey(KeyCode.LeftShift) && IsMoving();
        }

        public virtual bool IsAtack()
        {
            return Input.GetMouseButtonDown(0);
        }

        public virtual bool IsPaused()
        {
            return Input.GetKeyDown(KeyCode.Escape);
        }

        public bool MouseToWorldHitPoint(out RaycastHit hit, float maxCheckDistance = Mathf.Infinity)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out hit, maxCheckDistance);
        }
    }
}
#endif