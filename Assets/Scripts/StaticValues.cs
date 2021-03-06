﻿using Unity.Mathematics;

public class StaticValues 
{
    public static readonly float MAX_Y = 500f;
    public static readonly float MIN_Y = - 500f;

    public static readonly float SIZE = 1000f;

    public static readonly float MAX_X = SIZE;
    public static readonly float MIN_X = -SIZE;

    public static readonly float MAX_Z = SIZE;
    public static readonly float MIN_Z = -SIZE;

    public static readonly float AVOIDANCE_MIN_ANGLE = 5.24f; // 300 deg

    public static readonly float BUCKET_SIZE = 50f;

    public static readonly float NEIGHBOUR_RADIUS = 9f;
    public static readonly float FLOCK_RADIUS = 16f;
}
