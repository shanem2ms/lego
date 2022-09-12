#version 450
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout(set = 0, binding = 0) uniform texture2D Texture;
layout(set = 0, binding = 1) uniform sampler Sampler;

// Terrain Generation & Erosion

const float flowRate = .3;

const float evaporation = .01;
const float rain = .04;

const float drainage = 1.;
const float maxWater = 100.;

const float sedimentToHeight = .0003; // higher values have a faster but less realistic effect
const float sedimentPickUp = .1; // 1.0 => pick up as much sediment as possible at every step when flow is fast, .1 => have to flow 10 pixels to pick up that much
const float waterAsHeight = 0.;//.1; // this idea didn't really work

// prevent waterfalls causing infinitely powerful erosion
// this gives really nice control over how slopey (big values) or cliffy (small values) the terrain is
const float maxWaterSpeed = 3.; 

const vec2 initialImagePosition = vec2(.4,.4); // hit rewind when you change this
// you can also change this by clicking the mouse!

// vary erosion rate at different heights, and initialise with something with steps in it
// it's fun to change this while it's running
// so the initial state has no terraces but they appear due to different erosion rates
const float terraces = 0.0; //8.5;
const float terraceHardness = 5.; // -log2 of erosion rate of the hard layers: 0 = all terrain erodes at same rate, 1 = hard layers erode half as fast, 2 = one quarter as fast, etc

const float diagonalDist = 1./sqrt(2.); // don't change this

void TestOutFlow( inout float lowestSlope, inout int lowestIndex, in int indexMe, in int indexNeighbour, in float dist, in vec4 samples[25] )
{
    float slope = dot(vec2(waterAsHeight,1),samples[indexNeighbour].xw - samples[indexMe].xw) * dist;

    if ( slope < lowestSlope )
    {
        lowestSlope = slope;
        lowestIndex = indexNeighbour;
    }
}


void ProcessNeighbour
    (
        inout float outSlope,
        inout float inSlope,
        inout vec2 inFlow,
        in ivec2 index,
        in float dist,
        in vec4 samples[25]
    )
{
    int idx = index.y*5+index.x;
    int meIdx = 2*5+2;
    
    vec4 me = samples[meIdx];
    vec4 neighbour = samples[idx];
    
//    float slope = (neighbour.w-me.w) * dist; // can handle this if in caller
// MEASURE SLOPE INCLUDING WATER!!! HAHAHA! PREVENT SPIKES!
    float slope = dot(vec2(waterAsHeight,1),neighbour.xw-me.xw) * dist; // can handle this if in caller
    
    outSlope = min( outSlope, slope );
    
    float neighbourOutSlope = 0.;
    int neighbourOutSlopeTarget = -1;

	TestOutFlow( neighbourOutSlope, neighbourOutSlopeTarget, idx, idx-5-1, diagonalDist, samples );
	TestOutFlow( neighbourOutSlope, neighbourOutSlopeTarget, idx, idx-5+1, diagonalDist, samples );
	TestOutFlow( neighbourOutSlope, neighbourOutSlopeTarget, idx, idx+5-1, diagonalDist, samples );
	TestOutFlow( neighbourOutSlope, neighbourOutSlopeTarget, idx, idx+5+1, diagonalDist, samples );
	TestOutFlow( neighbourOutSlope, neighbourOutSlopeTarget, idx, idx-5, 1., samples );
	TestOutFlow( neighbourOutSlope, neighbourOutSlopeTarget, idx, idx+5, 1., samples );
	TestOutFlow( neighbourOutSlope, neighbourOutSlopeTarget, idx, idx-1, 1., samples );
	TestOutFlow( neighbourOutSlope, neighbourOutSlopeTarget, idx, idx+1, 1., samples );
    
    if ( neighbourOutSlopeTarget == meIdx )
    {
		inSlope += max(slope,0.); // do I want to count this for ones flowing away from me?
    	inFlow += neighbour.xy;
    }
}

vec4 mainImage( in ivec2 uv )
{
    vec4 fragColour = texelFetch(Texture,uv,0);
    
    /* erosion data:
    w = height
    x = amount of water
    y = amount of sediment

    z = flow speed (for rendering)
    */

    /*
    for each neighbour:
    check if I'm its lowest neighbour
    if I am, take all its water and sediment
    if I have any lower neighbour, dump all my previous water and sediment

    then evaporate (subtract const amount of water, down to 0)
    drop/pickup sediment as height (if sediment > water*slope, where slope = sum of incoming height deltas and outgoing height delta
    add rain (add const amount of water, even if 0)

    These have to be separate passes so outward slope reads new heights
    */

    // sample a 5x5 grid around the pixel
    vec4 samples[25];

    for ( int j=0; j < 5; j++ )
    {
        for ( int i=0; i < 5; i++ )
        {
            ivec2 tapUV = (uv+ivec2(i,j)-2);
            if ( tapUV.x == 0 && tapUV.y == 0 ) tapUV.x = 1; // don't read the data texel!
            samples[j*5+i] = texelFetch(Texture,tapUV,0);
        }
    }

    float outSlope = 1.; // height difference to lowest neighbour (negative)
    float inSlope = 0.; // sum of height differences to higher neighbours (positive)
    vec2 inFlow = vec2(0, 0); // sum of (water, sediment) from higher neighbours
    ProcessNeighbour( outSlope, inSlope, inFlow, ivec2(1,1), diagonalDist, samples );
    ProcessNeighbour( outSlope, inSlope, inFlow, ivec2(1,3), diagonalDist, samples );
    ProcessNeighbour( outSlope, inSlope, inFlow, ivec2(3,1), diagonalDist, samples );
    ProcessNeighbour( outSlope, inSlope, inFlow, ivec2(3,3), diagonalDist, samples );
    ProcessNeighbour( outSlope, inSlope, inFlow, ivec2(1,2), 1., samples );
    ProcessNeighbour( outSlope, inSlope, inFlow, ivec2(2,1), 1., samples );
    ProcessNeighbour( outSlope, inSlope, inFlow, ivec2(2,3), 1., samples );
    ProcessNeighbour( outSlope, inSlope, inFlow, ivec2(3,2), 1., samples );

    // modify water & sediment values
    if ( outSlope < 0. )
    {
        // zero my water and sediment - they'll be passed to a neighbour
        fragColour.xy *= 1.-flowRate;
    }

    fragColour.xy += inFlow*flowRate;

    // evaporate
    fragColour.x -= evaporation;
    fragColour.x = min(fragColour.x,maxWater);


    // compute speed
    const float slopeToSpeed = 1./.01; // denominator is height difference between input and output points at which we get a waterfall
    float speed = clamp( -outSlope * slopeToSpeed, 0., maxWaterSpeed ); // USE ONLY OUT - so water in dips is static! oops!

    // delete standing water? - no, that will drop a lot of sediment
    // but evaporate faster - as if soaking into water table
    if ( speed == 0. ) fragColour.x -= drainage;

    fragColour.z = speed;

    fragColour.x = max(fragColour.x,0.);

    // deposit/pick up sediment
    float maxSediment = fragColour.x * speed;

    if ( fragColour.y > maxSediment )
    {
        float deposit = fragColour.y-maxSediment;
        fragColour.w += deposit*sedimentToHeight; // deposit (perhaps clamp to inslope, or apply slowly)
        fragColour.y -= deposit;
    }
    else
    {
        float pickupRate = sedimentPickUp;
            
        if ( terraces != 0.0 )
        {
            // I think a log curve makes sense here, though I can't articulate why
            pickupRate *= exp2(-terraceHardness*(.5+.5*cos(fragColour.w*6.283185*terraces)));
        }
                               
        float pickup = min(min( maxSediment-fragColour.y, fragColour.w/sedimentToHeight ), pickupRate );
        fragColour.w -= pickup*sedimentToHeight;
        fragColour.y += pickup;
    }

    // rain
    fragColour.x += rain;

    return fragColour;
}

void main()
{
    vec4 outpixel = mainImage(gl_GlobalInvocationID.xy);
	imageStore(s_target, ivec2(gl_GlobalInvocationID.xy), outpixel);
}
