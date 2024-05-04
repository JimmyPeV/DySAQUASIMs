static const int3 offsets3D[27] =
{
	int3(-1, -1, -1),
	int3(-1, -1, 0),
	int3(-1, -1, 1),
	int3(-1, 0, -1),
	int3(-1, 0, 0),
	int3(-1, 0, 1),
	int3(-1, 1, -1),
	int3(-1, 1, 0),
	int3(-1, 1, 1),
	int3(0, -1, -1),
	int3(0, -1, 0),
	int3(0, -1, 1),
	int3(0, 0, -1),
	int3(0, 0, 0),
	int3(0, 0, 1),
	int3(0, 1, -1),
	int3(0, 1, 0),
	int3(0, 1, 1),
	int3(1, -1, -1),
	int3(1, -1, 0),
	int3(1, -1, 1),
	int3(1, 0, -1),
	int3(1, 0, 0),
	int3(1, 0, 1),
	int3(1, 1, -1),
	int3(1, 1, 0),
	int3(1, 1, 1)
};

// Constants used for hashing
static const uint hashK1 = 1301;
static const uint hashK2 = 5449;
static const uint hashK3 = 14983;

// Convert floating point position into an integer cell coordinate
int3 GetCell3D(float3 position, float radius)
{
	return (int3)floor(position / radius);
}

// Hash cell coordinate to a single unsigned integer
uint HashCell3D(int3 cell)
{
	cell = (uint3) cell;
	return (cell.x * hashK1) + (cell.y * hashK2) + (cell.z * hashK3);
}

uint KeyFromHash(uint hash, uint tableSize)
{
	return hash % tableSize;
}

/* Spatial 3D grid structure explanation
        Spatial 3D grid structure

        Spatial key lookup logics to speedup the process finding particles have impact to the sample point
        Consider there are 5 particles in the scene
        [0, 1, 2, 3, 4]
        For point 0
        Point index: 0
        Cell coord is: (2, 1, 5) based on point (x,y,z) and smoothingRadius
        To obtain the cell hash we multiply each coord with a prime number to evade repetitive cell keys, and then we sum them
        2* 1301 +
        1* 5449 +
        5* 14983;
        Cell hash is: 82966
        Cell key is obtained with the "Hash number" % "Number of points in scene"
        82966 % 5
        Cell key is: 2

        Same approach to all points
        So that in the spatialLookup array, it becomes
        [2, 2, 8, 1, 3]
        We can tell that having the same hash key values meaning those points are in the same cell grid
        Then we can short the spatialLookup array to have points same hash key group together
        pointsIndex: [0, 1, 2, 3, 4]
        pointsHashKey: [1, 2, 2, 3, 8] (sorted)

        The based on the pointsHashKey array we have, we can then generate a start index array for each hash key
        Start index: [0, 1, 1, 3, 4]
        Start index array provides a way to look up all points in the same grid
        For example, we calculate the hashkey for current grid is 0
        The startIndices[0] = 2, meaning that all points with hashKey 0 start from lookup array index 1
        So we have points [1, 2] are all in the same grid with hashKey 2

        Then for each sample point, we can first lcate which grid it is inside
        Then we calculate the total 3x3x3 cells around and including the center cell
        For each cell calculate the haskey
        The use startIndeces array to finde all points that inside of the cell
        For each point that reside inside of the 3x3x3 grid
        We then check if it is also inside of the smoothCircle of the sample point
        If inside, we then will update the properties of each point
*/