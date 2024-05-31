using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsMenu : MonoBehaviour
{
    #region setup
    public Simulation3D simulation3D;
    public Spawner spawner3D;
    public Display display3D;
    public UIManager uiManager;
    public CameraRotator camera;

    #endregion

    #region Simulation3D

    public void SetTimeScale(float timeScaleSimulation){
        simulation3D.timeScale = timeScaleSimulation;
        //Debug.Log(simulation3D.timeScale);
        uiManager.ShowMessage("Time Scale Set to: " + timeScaleSimulation);
        simulation3D.ResetSimulation();
    }

    public void SetIterationsByFrame(float iterations){
        simulation3D.iterationsByFrame = (int)iterations;
        //Debug.Log(simulation3D.iterationsByFrame);
        uiManager.ShowMessage("Iterations by frame Set to: " + iterations);
        simulation3D.ResetSimulation();
    }

    public void SetGravity(float grav){
        simulation3D.gravity = grav;
        uiManager.ShowMessage("Gravity Set to: " + grav);
        //Debug.Log(simulation3D.gravity);
    }

    public void SetCollisionDamping(float collisionDamp){
        simulation3D.collisionDamping = collisionDamp;
        uiManager.ShowMessage("Collision Damping Set to: " + collisionDamp);
        //Debug.Log(simulation3D.collisionDamping);
    }

    public void SetSmoothingRadius(float radius){
        simulation3D.smoothingRadius = radius;
        uiManager.ShowMessage("Smoothing Radius Set to: " + radius);
        //Beware this change might complicate calculations by frame
        //Debug.Log(simulation3D.smoothingRadius);
        simulation3D.ResetSimulation();
    }

    public void SetTargetDensity(float density){
        simulation3D.targetDensity = density;
        uiManager.ShowMessage("Density target Set to: " + density);
        //Debug.Log(simulation3D.targetDensity);
        simulation3D.ResetSimulation();
    }

    public void SetPressure(float pressure){
        simulation3D.pressureMultiplier = pressure;
        uiManager.ShowMessage("Pressure Set to: " + pressure);
        //Debug.Log(simulation3D.pressureMultiplier);
        simulation3D.ResetSimulation();
    }

    public void SetPressureMultiplier(float pressureMult){
        simulation3D.nearPressureMultiplier = pressureMult;
        uiManager.ShowMessage("Pressure Multiplier Set to: " + pressureMult);
        //Debug.Log(simulation3D.nearPressureMultiplier);
        simulation3D.ResetSimulation();
    }

    public void SetViscosityStrength(float viscosity){
        simulation3D.viscosityStrength = viscosity;
        uiManager.ShowMessage("Viscosity Set to: " + viscosity);
        //Debug.Log(simulation3D.viscosityStrength);
        simulation3D.ResetSimulation();
    }
    #endregion

    #region Spawner
    public void SetParticleQuantitySpawn(float particleQuantity){
        spawner3D.particleQuantityPerAxis = (int)particleQuantity;
        int howMany = spawner3D.particleQuantityPerAxis * spawner3D.particleQuantityPerAxis * spawner3D.particleQuantityPerAxis;
        uiManager.ShowMessage("Particles Spawned Set to: " + howMany);
        simulation3D.ResetSimulation();
    }
    public void SetSpawnSize(float spawnerSize){
        spawner3D.size = spawnerSize;
        uiManager.ShowMessage("Spawner Size Set to: " + spawnerSize);
        simulation3D.ResetSimulation();
    }
    public void SetSpawnRandomness(float randomness){
        spawner3D.jitterStrength = randomness;
        uiManager.ShowMessage("Spawner Randomness Set to: " + randomness);
        simulation3D.ResetSimulation();
    }

    #endregion

    #region Display
    public void SetParticleSize(float scale){
        display3D.meshScale = scale;
        uiManager.ShowMessage("Particle Mesh Size Set to: " + scale);
    }

    public void SetCameraDistance(float distanceCam){
        camera.distance = distanceCam;
    }

    public void SetCameraSpeed(float rotateCam){
        camera.speed = rotateCam;
    }
    /*public void SetParticleColorGradient(){

    }*/
    #endregion    
}
