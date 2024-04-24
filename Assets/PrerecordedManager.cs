using Holo.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrerecordedManager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        BasePipeline pcPipeline = BasePipeline.AddPipelineComponent(gameObject, UserRepresentationType.PC_PRERECORDED);
        pcPipeline.Init(new Player() { playerRepresentationType = UserRepresentationType.PC_PRERECORDED }, BasePipeline.SourceType.Self, true);
    }

}
