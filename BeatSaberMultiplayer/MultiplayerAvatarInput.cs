using CustomAvatar.Tracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer
{
    public class MultiplayerAvatarInput : AvatarInput
    {
        public bool poseValid;
        public bool fullBodyTracking;

        public Vector3 headPos;
        public Vector3 leftHandPos;
        public Vector3 rightHandPos;
        public Vector3 leftLegPos;
        public Vector3 rightLegPos;
        public Vector3 pelvisPos;

        public Quaternion headRot;
        public Quaternion leftHandRot;
        public Quaternion rightHandRot;
        public Quaternion leftLegRot;
        public Quaternion rightLegRot;
        public Quaternion pelvisRot;

        public override bool TryGetHeadPose(out Pose pose)
        {
            pose = new Pose(headPos, headRot);
            return poseValid;
        }

        public override bool TryGetLeftHandPose(out Pose pose)
        {
            pose = new Pose(leftHandPos, leftHandRot);
            return poseValid;
        }

        public override bool TryGetLeftFootPose(out Pose pose)
        {
            pose = new Pose(leftLegPos, leftLegRot);
            return poseValid && fullBodyTracking;
        }

        public override bool TryGetRightHandPose(out Pose pose)
        {
            pose = new Pose(rightHandPos, rightHandRot);
            return poseValid;
        }

        public override bool TryGetRightFootPose(out Pose pose)
        {
            pose = new Pose(rightLegPos, rightLegRot);
            return poseValid && fullBodyTracking;
        }

        public override bool TryGetWaistPose(out Pose pose)
        {
            pose = new Pose(pelvisPos, pelvisRot);
            return poseValid && fullBodyTracking;
        }
    }
}
