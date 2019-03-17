﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RagdollHelper : MonoBehaviour
{

    public PlayerBehaviour pB;
    Component[] components;

    //public property that can be set to toggle between ragdolled and animated character
    public bool ragdolled
    {
        get
        {
            return state != RagdollState.animated;
        }
        set
        {
            if (value == true)
            {
                if (state == RagdollState.animated)
                {
                    //Transition from animated to ragdolled
                    setKinematic(false); //allow the ragdoll RigidBodies to react to the environment
                    anim.enabled = false; //disable animation
                    state = RagdollState.ragdolled;
                }
            }
            else
            {
                if (state == RagdollState.ragdolled)
                {
                    //Transition from ragdolled to animated through the blendToAnim state
                    setKinematic(true); //disable gravity etc.
                    ragdollingEndTime = Time.time; //store the state change time
                    anim.enabled = true; //enable animation
                    state = RagdollState.blendToAnim;

                    //Store the ragdolled position for blending
                    foreach (BodyPart b in bodyParts)
                    {
                        b.storedRotation = b.transform.rotation;
                        b.storedPosition = b.transform.position;
                    }

                    //Remember some key positions
                    ragdolledFeetPosition = 0.5f * (anim.GetBoneTransform(HumanBodyBones.LeftToes).position + anim.GetBoneTransform(HumanBodyBones.RightToes).position);
                    ragdolledHeadPosition = anim.GetBoneTransform(HumanBodyBones.Head).position;
                    ragdolledHipPosition = anim.GetBoneTransform(HumanBodyBones.Hips).position;

                    //Initiate the get up animation
                    RaycastHit _sH;
                    
                    if (!Physics.SphereCast(pB.aimHelperSpine.position - pB.aimHelper.forward, 0.5f, pB.aimHelper.forward - Vector3.up * 0.2f, out _sH, 5)) //hip hips forward vector pointing upwards, initiate the get up from back animation
                    {
                        //anim.SetBool("GetUpFromBack",true);
                        anim.Play("Get Up Back");
                    }
                    else
                    {
                        //anim.SetBool("GetUpFromBelly",true);
                        anim.Play("Get Up Front");
                    }
                } //if (state==RagdollState.ragdolled)
            }   //if value==false	
        } //set
    }

    //Possible states of the ragdoll
    public enum RagdollState
    {
        animated,    //Mecanim is fully in control
        ragdolled,   //Mecanim turned off, physics controls the ragdoll
        blendToAnim  //Mecanim in control, but LateUpdate() is used to partially blend in the last ragdolled pose
    }

    //The current state
    public RagdollState state = RagdollState.animated;

    //How long do we blend when transitioning from ragdolled to animated
    public float ragdollToMecanimBlendTime = 0.3f;
    float mecanimToGetUpTransitionTime = 0.05f;

    //A helper variable to store the time when we transitioned from ragdolled to blendToAnim state
    float ragdollingEndTime = -100;

    //Declare a class that will hold useful information for each body part
    public class BodyPart
    {
        public Transform transform;
        public Vector3 storedPosition;
        public Quaternion storedRotation;
    }
    //Additional vectores for storing the pose the ragdoll ended up in.
    Vector3 ragdolledHipPosition, ragdolledHeadPosition, ragdolledFeetPosition;

    //Declare a list of body parts, initialized in Start()
    List<BodyPart> bodyParts = new List<BodyPart>();

    //Declare an Animator member variable, initialized in Start to point to this gameobject's Animator component.
    Animator anim;

    //A helper function to set the isKinematc property of all RigidBodies in the children of the 
    //game object that this script is attached to
    void setKinematic(bool newValue)
    {
        //Get an array of components that are of type Rigidbody
        Component[] components = GetComponentsInChildren(typeof(Rigidbody));

        //For each of the components in the array, treat the component as a Rigidbody and set its isKinematic property
        foreach (Component c in components)
        {
            (c as Rigidbody).isKinematic = newValue;
        }
    }

    // Initialization, first frame of game
    void Start()
    {
        //Set all RigidBodies to kinematic so that they can be controlled with Mecanim
        //and there will be no glitches when transitioning to a ragdoll
        setKinematic(true);

        //Find all the transforms in the character, assuming that this script is attached to the root

        components = GetComponentsInChildren(typeof(Transform));

        //For each of the transforms, create a BodyPart instance and store the transform 
        foreach (Component c in components)
        {
            if (c.GetComponent<Rigidbody>() != null)
            {
                BodyPart bodyPart = new BodyPart();
                bodyPart.transform = c as Transform;
                bodyParts.Add(bodyPart);
            }
        }
        // _components = null;
        //Store the Animator component
        anim = GetComponent<Animator>();
    }


    // Update is called once per frame
    void Update()
    {
    }

    void LateUpdate()
    {
        //Clear the get up animation controls so that we don't end up repeating the animations indefinitely

        //Blending from ragdoll back to animated
        if (state == RagdollState.blendToAnim)
        {
            if (Time.time <= ragdollingEndTime + mecanimToGetUpTransitionTime)
            {
                //If we are waiting for Mecanim to start playing the get up animations, update the root of the mecanim
                //character to the best match with the ragdoll
                Vector3 animatedToRagdolled = ragdolledHipPosition - anim.GetBoneTransform(HumanBodyBones.Hips).position;
                Vector3 newRootPosition = transform.position + animatedToRagdolled;

                //Now cast a ray from the computed position downwards and find the highest hit that does not belong to the character 
                RaycastHit[] hits = Physics.RaycastAll(new Ray(newRootPosition, Vector3.down));
                newRootPosition.y = 0;
                foreach (RaycastHit hit in hits)
                {
                    if (!hit.transform.IsChildOf(transform))
                    {
                        newRootPosition.y = Mathf.Max(newRootPosition.y, hit.point.y);
                    }
                }
                transform.position = newRootPosition;

                //Get body orientation in ground plane for both the ragdolled pose and the animated get up pose
                Vector3 ragdolledDirection = ragdolledHeadPosition - ragdolledFeetPosition;
                ragdolledDirection.y = 0;

                Vector3 meanFeetPosition = 0.5f * (anim.GetBoneTransform(HumanBodyBones.LeftFoot).position + anim.GetBoneTransform(HumanBodyBones.RightFoot).position);
                Vector3 animatedDirection = anim.GetBoneTransform(HumanBodyBones.Head).position - meanFeetPosition;
                animatedDirection.y = 0;

                //Try to match the rotations. Note that we can only rotate around Y axis, as the animated characted must stay upright,
                //hence setting the y components of the vectors to zero. 
                transform.rotation *= Quaternion.FromToRotation(animatedDirection.normalized, ragdolledDirection.normalized);
            }
            //compute the ragdoll blend amount in the range 0...1
            float ragdollBlendAmount = 1f - (Time.time - ragdollingEndTime - mecanimToGetUpTransitionTime) / ragdollToMecanimBlendTime;
            ragdollBlendAmount = Mathf.Clamp01(ragdollBlendAmount);

            //In LateUpdate(), Mecanim has already updated the body pose according to the animations. 
            //To enable smooth transitioning from a ragdoll to animation, we lerp the position of the hips 
            //and slerp all the rotations towards the ones stored when ending the ragdolling
            foreach (BodyPart b in bodyParts)
            {
                if (b.transform != transform)
                { //this if is to prevent us from modifying the root of the character, only the actual body parts
                  //position is only interpolated for the hips
                    if (b.transform == anim.GetBoneTransform(HumanBodyBones.Hips))
                        b.transform.position = Vector3.Lerp(b.transform.position, b.storedPosition, ragdollBlendAmount);
                    //rotation is interpolated for all body parts
                    b.transform.rotation = Quaternion.Lerp(b.transform.rotation, b.storedRotation, ragdollBlendAmount);
                }
            }

            pB.rb.velocity = Vector3.zero;
            pB.rb.isKinematic = false;
            pB.rb.useGravity = true;
            if (ragdollBlendAmount == 0)
            {
                state = RagdollState.animated;
                
                return;
            }
        }
    }
}