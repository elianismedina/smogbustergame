using System;
using UnityEngine;

/// <summary>
/// Controlador PID escalar (basado en el tutorial de Habrador).
/// output = P*error + I*sum(error*dt) + D*d(error)/dt
/// </summary>
[Serializable]
public class PIDController
{
    public float Kp = 1f;
    public float Ki = 0f;
    public float Kd = 0f;

    [Tooltip("Límite del término integral (anti-windup).")]
    public float IntegralLimit = 20f;

    private float _integral;
    private float _previousError;
    private bool _hasPrevious;

    public PIDController() { }

    public PIDController(float kp, float ki, float kd, float integralLimit = 20f)
    {
        Kp = kp;
        Ki = ki;
        Kd = kd;
        IntegralLimit = integralLimit;
    }

    public float Update(float error, float dt)
    {
        if (dt <= Mathf.Epsilon)
        {
            return 0f;
        }

        _integral = Mathf.Clamp(_integral + error * dt, -IntegralLimit, IntegralLimit);

        // Sin muestra previa no hay derivada válida (evita un pico en el primer frame).
        float derivative = _hasPrevious ? (error - _previousError) / dt : 0f;
        _previousError = error;
        _hasPrevious = true;

        return Kp * error + Ki * _integral + Kd * derivative;
    }

    public void Reset()
    {
        _integral = 0f;
        _previousError = 0f;
        _hasPrevious = false;
    }
}
