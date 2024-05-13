using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsMenu : MonoBehaviour
{
    #region setup
    public Simulation3D simulation3D;
    public Spawner spawner3D;
    public Display display3D;

    #endregion

    #region Simulation3D

    public void SetTimeScale(float timeScaleSimulation){
        simulation3D.timeScale = timeScaleSimulation;
        //Debug.Log(simulation3D.timeScale);
        simulation3D.ResetSimulation();
    }

    public void SetIterationsByFrame(float iterations){
        simulation3D.iterationsByFrame = (int)iterations;
        //Debug.Log(simulation3D.iterationsByFrame);
        simulation3D.ResetSimulation();
    }

    public void SetGravity(float grav){
        simulation3D.gravity = grav;
        //Debug.Log(simulation3D.gravity);
    }

    public void SetCollisionDamping(float collisionDamp){
        simulation3D.collisionDamping = collisionDamp;
        //Debug.Log(simulation3D.collisionDamping);
    }

    public void SetSmoothingRadius(float radius){
        simulation3D.smoothingRadius = radius;
        //Beware this change might complicate calculations by frame
        //Debug.Log(simulation3D.smoothingRadius);
        simulation3D.ResetSimulation();
    }

    public void SetTargetDensity(float density){
        simulation3D.targetDensity = density;
        //Debug.Log(simulation3D.targetDensity);
        simulation3D.ResetSimulation();
    }

    public void SetPressure(float pressure){
        simulation3D.pressureMultiplier = pressure;
        //Debug.Log(simulation3D.pressureMultiplier);
        simulation3D.ResetSimulation();
    }

    public void SetPressureMultiplier(float pressureMult){
        simulation3D.nearPressureMultiplier = pressureMult;
        //Debug.Log(simulation3D.nearPressureMultiplier);
        simulation3D.ResetSimulation();
    }

    public void SetViscosityStrength(float viscosity){
        simulation3D.viscosityStrength = viscosity;
        //Debug.Log(simulation3D.viscosityStrength);
        simulation3D.ResetSimulation();
    }
    #endregion

    #region Spawner
    public void SetParticleQuantitySpawn(float particleQuantity){
        spawner3D.particleQuantityPerAxis = (int)particleQuantity;
        simulation3D.ResetSimulation();
    }
    public void SetSpawnSize(float spawnerSize){
        spawner3D.size = spawnerSize;
        simulation3D.ResetSimulation();
    }
    public void SetSpawnRandomness(float randomness){
        spawner3D.jitterStrength = randomness;
        simulation3D.ResetSimulation();
    }

    #endregion

    #region Display
    public void SetParticleSize(float scale){
        display3D.meshScale = scale;
    }
    /*public void SetParticleColorGradient(){

    }*/
    #endregion    
}
