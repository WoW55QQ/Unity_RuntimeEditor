using System.Collections.Generic;
using ProtoBuf;
using Battlehub.RTSaveLoad2;
using UnityEngine;
using UnityEngine.Battlehub.SL2;
using System;

using UnityObject = UnityEngine.Object;
namespace UnityEngine.Battlehub.SL2
{
    [ProtoContract(AsReferenceDefault = true)]
    public partial class PersistentMeshFilter : PersistentObject
    {
        [ProtoMember(256)]
        public long sharedMesh;

        protected override void ReadFromImpl(object obj)
        {
            base.ReadFromImpl(obj);
            MeshFilter uo = (MeshFilter)obj;
            sharedMesh = ToID(uo.sharedMesh);
        }

        protected override object WriteToImpl(object obj)
        {
            obj = base.WriteToImpl(obj);
            MeshFilter uo = (MeshFilter)obj;
            uo.sharedMesh = FromID<Mesh>(sharedMesh);
            return obj;
        }

        protected override void GetDepsImpl(GetDepsContext context)
        {
            AddDep(sharedMesh, context);
        }

        protected override void GetDepsFromImpl(object obj, GetDepsFromContext context)
        {
            MeshFilter uo = (MeshFilter)obj;
            AddDep(uo.sharedMesh, context);
        }

        partial void OnBeforeReadFrom(object obj);
        partial void OnAfterReadFrom(object obj);
        public override void ReadFrom(object obj)
        {
            OnBeforeReadFrom(obj);
            ReadFrom(obj);
            OnAfterReadFrom(obj);
        }

        partial void OnBeforeWriteTo(ref object input);
        partial void OnAfterWriteTo(ref object input);
        public override object WriteTo(object obj)
        {
           OnBeforeWriteTo(ref obj);
           obj = WriteTo(obj);
           OnAfterWriteTo(ref obj);
           return obj;
        }

        partial void OnBeforeGetDeps(GetDepsContext context);
        partial void OnAfterGetDeps(GetDepsContext context);
        public override void GetDeps(GetDepsContext context)
        {
           OnBeforeGetDeps(context);
           GetDepsImpl(context);
           OnAfterGetDeps(context);
        }

        partial void OnBeforeGetDepsFrom(object obj, GetDepsFromContext context);
        partial void OnAfterGetDepsFrom(object obj, GetDepsFromContext context);
        public override void GetDepsFrom(object obj, GetDepsFromContext context)
        {
           OnBeforeGetDepsFrom(obj, context);
           GetDepsFromImpl(obj, context);
           OnAfterGetDepsFrom(obj, context);
        }

        public static implicit operator MeshFilter(PersistentMeshFilter surrogate)
        {
            return (MeshFilter)surrogate.WriteTo(new MeshFilter());
        }
        
        public static implicit operator PersistentMeshFilter(MeshFilter obj)
        {
            PersistentMeshFilter surrogate = new PersistentMeshFilter();
            surrogate.ReadFrom(obj);
            return surrogate;
        }
    }
}

