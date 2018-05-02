﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControl : MonoBehaviour {

    [Tooltip("A gameobject containing child gameobjects tranformed at the x-value of each lane respectively")]
    public GameObject LaneOriginPoints;

    [Tooltip("A distance close enough to consider for locking the player into a lane")]
    public float CloseEnoughToLaneDistance;

    [Tooltip("Movement magnitude factor")]
    public float MovementMagnitudeFactor;

    [Tooltip("Jump magnitude factor")]
    public float JumpMagnitudeFactor;

    [Tooltip("Gravity magnitude factor")]
    public float GravityMagnitudeFactor;

    [Tooltip("The clockwise angle that the player will be sliding in")]
    public float SlidingAngle;

    [Tooltip("The number of seconds that each sliding occurs")]
    public float SlideTime;

    [Tooltip("The player's distance to the origin")]
    public float GroundLevelMargin;

    [Tooltip("Reference to the camera")]
    public Transform CameraReference;
    private CapsuleCollider collider;
    private bool isGrounded;
    private bool isJumping;
    private float laneLockingTimeout;
    private int currentLane;
    private Vector3 laneMovementDirection;
    private bool isSliding;
    private float slidingTimer;
    private bool slideFromAir;
    private float verticalVelocity;
    private Animator animator;

    private TouchPlot plot;

    private WorldMover worldMover;

    private float distanceBetweenEachLane;

    private Quaternion enforcedRotation;

    // Use this for initialization

    void Start(){
        laneMovementDirection = Vector3.zero;
        currentLane = 1;
        slideFromAir = false;
        collider = this.gameObject.GetComponent<CapsuleCollider>();
        plot = new TouchPlot();
        worldMover = GameObject.Find("WorldMover").GetComponent<WorldMover>();
        animator = GetComponent<Animator>();

        Func<int, int, float>  calcDistanceBetweenLane = (l1, l2) => Mathf.Abs(
            LaneOriginPoints.transform.GetChild(l2).transform.position.x - 
            LaneOriginPoints.transform.GetChild(l1).transform.position.x
        );

        for(int i = 1; i < LaneOriginPoints.transform.childCount; i++){
            if (i < 2)
                distanceBetweenEachLane = calcDistanceBetweenLane(i - 1, i);
            else if(Debug.isDebugBuild)
                Debug.Assert(calcDistanceBetweenLane(i - 1, i) == distanceBetweenEachLane, "The distance between lanes must consistent");
        }
    }

    // Update is called once per frame
    void Update () {
        enforcedRotation = transform.rotation;

        // Stay on the floor
        // #################
        
        RaycastHit hit;
        float distanceToGround = GroundLevelMargin + getEllipsRadius();

        isGrounded = Physics.Raycast(new Ray(transform.position + collider.center, Vector3.down), out hit, distanceToGround);

        if (isGrounded)
        {
            animator.SetTrigger("Ground");
            transform.position = new Vector3(
                    transform.position.x,
                    Mathf.Max(hit.point.y + distanceToGround, transform.position.y),
                    transform.position.z
                );
        }
        

        // Touch & Keyboard Input
        // ######################

        TouchPlot.SwipeDirection swipe = plot.GetSwipeDirection(25f);
        bool pressingLeft  = Input.GetKeyDown(KeyCode.LeftArrow)  || swipe == TouchPlot.SwipeDirection.LEFT,
             pressingRight = Input.GetKeyDown(KeyCode.RightArrow) || swipe == TouchPlot.SwipeDirection.RIGHT,
             pressingUp    = Input.GetKeyDown(KeyCode.Space)      || swipe == TouchPlot.SwipeDirection.UP,
             pressingDown  = Input.GetKeyDown(KeyCode.DownArrow)  || swipe == TouchPlot.SwipeDirection.DOWN;

        // Lane movement
        // #############

        if (pressingLeft)
        {
            animator.SetTrigger("Left");
            moveToLane(currentLane - 1);

        }
        else if (pressingRight)
        {
            animator.SetTrigger("Right");
            moveToLane(currentLane + 1);
        }

        Vector3 euler = transform.rotation.eulerAngles;
        if (laneMovementDirection != Vector3.zero) {
            float xDestination =  LaneOriginPoints.transform.GetChild(currentLane).transform.position.x;
            float xSpeed = Mathf.Min(MovementMagnitudeFactor * Mathf.Abs(xDestination - transform.position.x), 100f);
            
            // This will make the player look in the direction of the lane that the player moves towards:
            euler.y = laneMovementDirection.x * 70 * Mathf.Clamp(xSpeed / 10f, 0f, 1f);    

            // This will do the actual move of the player in the direction of the next lane:
            transform.position += laneMovementDirection * xSpeed * Time.deltaTime;

            if (laneLockingTimeout <= 0) {
                foreach(Transform laneTransf in LaneOriginPoints.transform) {
                    if (laneLockingTimeout <= 0 && Mathf.Abs(transform.position.x - laneTransf.position.x) < CloseEnoughToLaneDistance)  
                        transform.position = new Vector3(laneTransf.position.x, transform.position.y, transform.position.z);
                }

                laneMovementDirection = Vector3.zero;
            }
        }
        else {
            euler.y = 0f;
        }

        euler.x += getTemporaryAngleDelta(euler.x, SlidingAngle, ref isSliding, slidingTimer > 0);
        enforcedRotation = Quaternion.Euler(euler);

        // Jumping & gravity calculation
        // #############################
        
        if (isGrounded){
            // On the ground:
            
            isJumping = false;
            

            if (pressingUp) {
                animator.ResetTrigger("Ground");
                verticalVelocity = JumpMagnitudeFactor;
                slidingTimer = 0f;
                slideFromAir = false;
                isJumping = true;
                animator.SetTrigger("Jump");
            }
            else{
                if (pressingDown || slideFromAir){
                    slidingTimer = SlideTime;
                    slideFromAir = false;
                    enforcedRotation = Quaternion.identity;
                    animator.SetTrigger("Slide");
                }

                verticalVelocity = Mathf.Max(0, verticalVelocity);    
            }

            if (!isJumping && !isSliding) ;
                
               // enforcedRotation.Set(0f, enforcedRotation.y, enforcedRotation.z, enforcedRotation.w);
        }
        else {
            // In the air:
            animator.ResetTrigger("Left");
            animator.ResetTrigger("Right");
            if (pressingDown) {
                verticalVelocity = -4f * JumpMagnitudeFactor;
                slideFromAir = true;
                

            }
            else
                verticalVelocity -= GravityMagnitudeFactor * Time.deltaTime;
        }

        // Apply the new transformations
        transform.position += Vector3.up * verticalVelocity * Time.deltaTime;

        laneLockingTimeout -= Time.deltaTime;
        slidingTimer -= Time.deltaTime;
	}

    void LateUpdate() {
        if (isSliding || slideFromAir) {
            animator.ResetTrigger("Left");
            animator.ResetTrigger("Right");

        }
        //    transform.rotation = enforcedRotation;

        if (!isJumping) {
            CameraReference.position = new Vector3(
                CameraReference.position.x, 
                Mathf.Max(transform.position.y, 0), 
                CameraReference.position.z
            );
        }
    }

    public float getTemporaryAngleDelta(float currentClockwiseAngle, float requestedClockwiseAngle, ref bool activeMotion, bool keepInNewPosition) {
        float a = 0, b = 0;
        const float speedFactor = 10f;
        
        if (keepInNewPosition) {
            a = (requestedClockwiseAngle - currentClockwiseAngle) * Time.deltaTime * speedFactor;
            b = (requestedClockwiseAngle - currentClockwiseAngle - 360) * Time.deltaTime * speedFactor;
            activeMotion = true; 
        }
        else if (activeMotion) {     
            a = -currentClockwiseAngle;
            b = 360 - currentClockwiseAngle;
            activeMotion = Mathf.Abs(currentClockwiseAngle) > 10f && Mathf.Abs(360 - currentClockwiseAngle) > 10f;

            if (activeMotion){
                a *= Time.deltaTime * speedFactor;
                b *= Time.deltaTime * speedFactor;
            }    
        }
        
        return (Mathf.Abs(a) < Mathf.Abs(b) ? a : b);
    }

    public void moveToLane(int lane) {
        int validNewLane = Mathf.Clamp(lane, 0, LaneOriginPoints.transform.childCount - 1);
        
        if (currentLane != validNewLane) {
            laneMovementDirection = (validNewLane < currentLane ? Vector3.left : Vector3.right);

            if (Physics.Raycast(new Ray(transform.position + collider.center, laneMovementDirection), distanceBetweenEachLane)) {
                // Obstacle in the way
                laneMovementDirection = Vector3.zero;
                return;
            }

            laneLockingTimeout = 0.5f;
            currentLane = validNewLane;   
        }
    }

    private float getEllipsRadius() {
        float a = collider.height / 2;
        float b = collider.radius;
        float angle = Mathf.Deg2Rad * transform.rotation.eulerAngles.x;

        return (
            a * b / 
            Mathf.Sqrt(
                a * a * Mathf.Pow(Mathf.Sin(angle), 2f) + 
                b * b * Mathf.Pow(Mathf.Cos(angle), 2f)
            )
        );
    }
}
