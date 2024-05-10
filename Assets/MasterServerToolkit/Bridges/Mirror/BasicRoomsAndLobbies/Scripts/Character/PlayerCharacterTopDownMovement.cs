#if MIRROR
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacterInput), typeof(CharacterController))]
    public class PlayerCharacterTopDownMovement : PlayerCharacterMovement
    {
        protected override void UpdateMovement()
        {
            if (!characterController.enabled) return;

            if (characterController.isGrounded)
            {
                var aimDirection = lookController.AimDirection();

                // If we are moving but not armed mode
                if (inputController.IsMoving() && !inputController.IsArmed())
                {
                    // Calculate new angle of player
                    Vector3 currentDirection = inputController.MovementAxisDirection();

                    // 
                    if (!currentDirection.Equals(Vector3.zero))
                    {
                        playerTargetDirectionAngle = Quaternion.LookRotation(currentDirection) * lookController.GetCameraRotation();
                    }
                }
                // If we are moving and armed mode
                else if (inputController.IsMoving() && inputController.IsArmed())
                {
                    playerTargetDirectionAngle = Quaternion.LookRotation(new Vector3(aimDirection.x, 0f, aimDirection.z));
                }
                // If we are not moving and not armed mode
                else if (!inputController.IsMoving() && inputController.IsArmed())
                {
                    playerTargetDirectionAngle = Quaternion.LookRotation(new Vector3(aimDirection.x, 0f, aimDirection.z));
                }

                // 
                if (movementIsAllowed)
                {
                    // Rotate character to target direction
                    transform.rotation = Quaternion.Lerp(transform.rotation, playerTargetDirectionAngle, Time.deltaTime * rotationSmoothTime);
                }

                // Let's calculate input direction
                var inputAxisAngle = inputController.MovementAxisDirection().Equals(Vector3.zero) ? Vector3.zero : Quaternion.LookRotation(inputController.MovementAxisDirection()).eulerAngles;

                //
                var compositeAngle = inputAxisAngle - transform.eulerAngles;

                // 
                calculatedInputDirection = Quaternion.Euler(compositeAngle) * lookController.GetCameraRotation() * transform.forward * inputController.MovementAxisMagnitude();

                // 
                calculatedMovementDirection.y = -stickToGroundPower;
                calculatedMovementDirection.x = calculatedInputDirection.x * CurrentMovementSpeed;
                calculatedMovementDirection.z = calculatedInputDirection.z * CurrentMovementSpeed;

                // 
                if (inputController.IsJump() && IsJumpAvailable)
                {
                    calculatedMovementDirection.y = jumpPower;
                    nextJumpTime = Time.time + jumpRate;
                }
            }
            else
            {
                calculatedMovementDirection += gravityMultiplier * Time.deltaTime * Physics.gravity;
            }

            characterController.Move(calculatedMovementDirection * Time.deltaTime);
        }
    }
}
#endif