using System.Collections;
using UnityEngine;

namespace NsmbDs {
    /// <summary>Plays the base project's Mario model from the motor's state, like its MarioPlayerAnimator without Quantum.</summary>
    [RequireComponent(typeof(NsmbPlayer))]
    public sealed class NsmbPlayerAnimator : MonoBehaviour {
        private const string ModelPath = "ModelParent/Models";
        private const float SideAngle = 67.5f;
        private const float MaxRotationPerSecond = 2000f;
        private const float WalkAnimationSpeed = 2f;
        private const float IceAnimationSpeed = 2.7f;
        private const float BlinkFrameSeconds = 0.1f;
        private const float SmallModelScale = 0.5f;
        private const int SwimLayer = 3;
        private const int EyesNormal = 0;
        private const int EyesHalfBlink = 1;
        private const int EyesFullBlink = 2;

        private static readonly int StateFalling = Animator.StringToHash("falling");
        private static readonly int StateTripleJump = Animator.StringToHash("triplejump");
        private static readonly int StateJumpLanding = Animator.StringToHash("jumplanding");
        private static readonly int StateJumpLandingEdge = Animator.StringToHash("jumplanding-edge");
        private static readonly int ParamEyeState = Shader.PropertyToID("_EyeState");
        private static readonly int ParamModelScale = Shader.PropertyToID("ModelScale");

        private NsmbPlayer player;
        private NsmbPlayerMotor motor;
        private Animator animator;
        private Transform modelRoot;
        private Renderer[] renderers;
        private MaterialPropertyBlock materialBlock;
        private Quaternion rotationTarget;
        private bool rotateInstantly;
        private bool wasTurnaround;
        private int eyeState;

        private void Awake() {
            player = GetComponent<NsmbPlayer>();
            animator = GetComponent<Animator>();
            modelRoot = transform.Find(ModelPath);
            renderers = GetComponentsInChildren<Renderer>(true);
            materialBlock = new MaterialPropertyBlock();
            rotationTarget = Quaternion.Euler(0f, 180f - SideAngle, 0f);
            StartCoroutine(BlinkRoutine());
        }

        private void LateUpdate() {
            if (player.Motor != motor) {
                Follow(player.Motor);
            }
            if (motor == null) {
                return;
            }

            UpdateParameters();
            UpdateFacing();
            materialBlock.SetFloat(ParamEyeState, eyeState);
            materialBlock.SetFloat(ParamModelScale, modelRoot.lossyScale.x * SmallModelScale);
            foreach (Renderer model in renderers) {
                model.SetPropertyBlock(materialBlock);
            }
        }

        private void Follow(NsmbPlayerMotor newMotor) {
            if (motor != null) {
                motor.LandedStill -= OnLandedStill;
                motor.Walljumped -= OnWalljumped;
                motor.Paddled -= OnPaddled;
            }

            motor = newMotor;
            if (motor != null) {
                motor.LandedStill += OnLandedStill;
                motor.Walljumped += OnWalljumped;
                motor.Paddled += OnPaddled;
            }
        }

        private void UpdateParameters() {
            NsmbPlayerInput input = player.CurrentInput;
            bool left = input.Left;
            bool right = input.Right;

            animator.SetBool("onLeft", motor.WallslideLeft);
            animator.SetBool("onRight", motor.WallslideRight);
            animator.SetBool("onGround", motor.Grounded || motor.CoyoteTime);
            animator.SetBool("skidding", motor.Skidding);
            animator.SetBool("crouching", motor.Crouching);
            animator.SetBool("groundpound", motor.Groundpounding);
            animator.SetBool("sliding", motor.Sliding);
            animator.SetBool("facingRight", (left ^ right) ? right : motor.FacingRight);
            animator.SetBool("doublejump", motor.JumpState == NsmbJumpState.Double);
            animator.SetBool("triplejump", motor.JumpState == NsmbJumpState.Triple);
            animator.SetBool("pipe", player.InWarp);
            animator.SetBool("turnaround", motor.Turnaround);
            animator.SetBool("swimming", motor.Underwater && !motor.Groundpounding);
            animator.SetBool("a_held", input.Jump);
            animator.SetBool("pushing", (right && motor.TouchingRightWall) || (left && motor.TouchingLeftWall));
            animator.SetLayerWeight(SwimLayer, motor.Underwater ? 1f : 0f);

            float animatedVelocity = Mathf.Abs(motor.VelocityX);
            if (left ^ right && !motor.TouchingRightWall && !motor.TouchingLeftWall && !motor.GroundpoundStanding) {
                animatedVelocity = Mathf.Max(motor.OnSlippery ? IceAnimationSpeed : WalkAnimationSpeed, animatedVelocity);
            } else if (motor.OnSlippery) {
                animatedVelocity = 0f;
            }
            if (animatedVelocity < 0.03f) {
                animatedVelocity = 0f;
            }

            animator.SetFloat("velocityX", animatedVelocity);
            animator.SetFloat("velocityY", motor.VelocityY);
            animator.SetFloat("velocityMagnitude", new Vector2(motor.VelocityX, motor.VelocityY).magnitude);
        }

        private void UpdateFacing() {
            float angleRight = 180f - SideAngle;
            float angleLeft = 180f + SideAngle;
            bool turning = animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround");

            rotateInstantly = false;
            if (player.InWarp) {
                rotationTarget = Quaternion.Euler(0f, motor.FacingRight ? angleRight : angleLeft, 0f);
                rotateInstantly = true;
            } else if (wasTurnaround || motor.Skidding || motor.Turnaround || turning) {
                bool flip = motor.FacingRight ^ (turning || motor.Skidding);
                rotationTarget = Quaternion.Euler(0f, flip ? angleLeft : angleRight, 0f);
                rotateInstantly = true;
            } else if (motor.Wallsliding) {
                rotationTarget = Quaternion.Euler(0f, motor.WallslideRight ? angleRight : angleLeft, 0f);
            } else {
                rotationTarget = Quaternion.Euler(0f, motor.FacingRight ? angleRight : angleLeft, 0f);
            }
            wasTurnaround = motor.Turnaround;

            modelRoot.rotation = rotateInstantly
                ? rotationTarget
                : Quaternion.RotateTowards(modelRoot.rotation, rotationTarget, MaxRotationPerSecond * Time.deltaTime);
        }

        private void OnLandedStill() {
            int state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (state == StateFalling) {
                animator.Play(StateJumpLanding);
            } else if (state == StateTripleJump) {
                animator.Play(StateJumpLandingEdge);
            }
        }

        private void OnWalljumped() {
            animator.SetTrigger("walljump");
        }

        private void OnPaddled() {
            animator.SetTrigger("paddle");
        }

        private IEnumerator BlinkRoutine() {
            WaitForSeconds frame = new(BlinkFrameSeconds);
            while (true) {
                yield return new WaitForSeconds(3f + (Random.value * 6f));
                eyeState = EyesHalfBlink;
                yield return frame;
                eyeState = EyesFullBlink;
                yield return frame;
                eyeState = EyesHalfBlink;
                yield return frame;
                eyeState = EyesNormal;
            }
        }
    }
}
