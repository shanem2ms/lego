#include "StdIncludes.h"
#include "World.h"
#include "Application.h"
#include "Application.h"
#include "Engine.h"
#include <numeric>
#include "Mesh.h"
#include "OctTile.h"
#include "Frustum.h"
#include "gmtl/PlaneOps.h"
#include "LegoBrick.h"
#include "PartDefs.h"
#include "BrickMgr.h"
#include "Physics.h"
#include "Audio.h"
#include "PlayerView.h"
#include "gmtl/AABoxOps.h"
#include "MbxImport.h"
#define NOMINMAX


using namespace gmtl;
const char* ldrpath = "C:\\ldraw";

namespace sam
{     
    World::World() :
        m_width(-1),
        m_height(-1),
        m_currentTool(0),
        m_pPickedBrick(nullptr),
        m_debugDraw(0),
        m_disableCollisionCheck(false),
        m_player(std::make_shared<Player>()),
        m_level(false)
    {        
    }  

    void World::Open(const std::string& path)
    {
        m_level.OpenDb(path);
    }

    class Touch
    {
        Point2f m_touch;
        Point2f m_lastDragPos;
        bool m_isInit;
        bool m_isDragging;
    public:

        Vec2f m_initCamDir;

        Touch() : m_isInit(false),
            m_isDragging(false) {}

        bool IsInit() const {
            return m_isInit;
        }
        void SetInitialPos(const Point2f& mouse)
        {
            m_touch = mouse;
            m_isInit = true;
        }

        bool IsDragging() const { return m_isDragging; }

        void SetDragPos(const Point2f& newTouchPt)
        {
            Vec2f dpt = (newTouchPt - m_touch);
            if (length(dpt) > 0.04)
                m_isDragging = true;
            m_lastDragPos = newTouchPt;
        }

        const Point2f& LastDragPos() const { return m_lastDragPos; }
        const Point2f& TouchPos() const { return m_touch; }        
    };
    //https://shanetest-cos-earth.s3.us-east.cloud-object-storage.appdomain.cloud/usa10m_whqt/Q0/L0/R0/C0
    //https://shanetest-cos-earth.s3.us-east.cloud-object-storage.appdomain.cloud/world9m_whqt/Q0/L0/R0/C0
    //https://shanetest-cos-earth.s3.us-east.cloud-object-storage.appdomain.cloud/world9m_whqt/Q1/L3/R1/Q1_L3_R1_C0.png

   
    bool cursormode = false;
    void World::MouseDown(float x, float y, int buttonId)
    {
        if (buttonId == 1 && m_pPickedBrick != nullptr)
        {
            m_connectionLogic.PlaceBrick(m_player, m_pPickedBrick,
                m_octTileSelection, !m_disableCollisionCheck);
        }
        else if (buttonId == 0 && m_pPickedBrick != nullptr)
        {
            const PartInst &pi = m_pPickedBrick->GetPartInst();
            if (pi.canBeDestroyed)
            {
                Matrix44f wm = m_pPickedBrick->GetWorldMatrix();
                Vec4f offset;
                xform(offset, wm, Vec4f(0, 0, 0, 1));                
                Application::Inst().GetAudio().PlayOnce("break.mp3");
                PartInst piAdj = pi;
                piAdj.pos = Vec3f(offset);
                m_octTileSelection.RemovePart(piAdj);
            }
        }
    }

    constexpr float pi_over_two = 3.14159265358979323846f * 0.5f;
    void World::RawMove(float dx, float dy)
    {
        m_player->RawMove(dx, dy);
    }

    void World::MouseDrag(float x, float y, int buttonId)
    {
      
    }

    void World::WheelScroll(float delta)
    {
        m_player->WheelScroll(delta);
    }


    void World::MouseUp(int buttonId)
    {        
    }
   
    static int curPartIdx = 0;
    static int prevPartIdx = -1;

    const int LeftShift = 16;
    const int LeftCtrl = 17;
    const int SpaceBar = 32;
    const int AButton = 'A';
    const int DButton = 'D';
    const int WButton = 'W';
    const int SButton = 'S';
    const int FButton = 'F';
    bool isPaused = false;
    int partChange = 0;

    int g_maxTileLod = 8;
    void World::KeyDown(int k)
    {
        switch (k)
        {
        case 'P':
            BrickManager::Inst().LoadPrimitives(
                BrickManager::Inst().GetBrick(m_player->GetRightHandPart().id));
            break;
        case 'B':
            m_debugDraw = (m_debugDraw + 1) % 3;
            break;
        case 'N':
            partChange = 1;
            break;
        case 'L':
            partChange = -1;
            break;
        case 'Q':
        {
            PartInst part = m_player->GetRightHandPart();
            part.rot *= make<Quatf>(AxisAnglef(
                -Math::PI_OVER_2, Vec3f(1, 0, 0)));
            m_player->SetRightHandPart(part);
            break;
        }
        case 'O':
            m_physics->SetPhysicsDbg(
                !m_physics->GetPhysicsDbg());
            break;
        case 'R':
        {
            PartInst part = m_player->GetRightHandPart();
            part.rot *= make<Quatf>(AxisAnglef(
                -Math::PI_OVER_2, Vec3f(0, 1, 0)));
            m_player->SetRightHandPart(part);
            break;
        }
        case 'I':
            {
                std::vector<PartInst> piImport;
                MbxImport mbxImport;
                mbxImport.ImportFile(Application::Inst().Documents() + "/Import/Trixie and Starlight.json", 
                    m_player->Pos(), piImport);
                for (const auto& pi : piImport)
                {
                    m_octTileSelection.AddPartInst(pi);
                }
            }
            break;
        case 'E':
        {
            m_showInventoryFn();
            break;
        }
        case LeftCtrl:
            m_disableCollisionCheck = true;
            break;
        }
        m_player->KeyDown(k);
    } 

    void World::KeyUp(int k)
    {
        switch (k)
        {
        case LeftCtrl:
            m_disableCollisionCheck = false;
            break;
        case 'N':
        case 'L':
            partChange = 0;
            break;
        }
        m_player->KeyUp(k);
    }

    std::string g_partName;
    Loc g_hitLoc(0, 0, 0);
    float g_hitLocArea = 0;
    float g_hitDist = 0;
    bool g_doImport = true;
    Loc g_inLoc{ 0,0,0 };
    void World::Update(Engine& e, DrawContext& ctx)
    {
        ctx.debugDraw = m_debugDraw;
        if (m_octTiles == nullptr)
        {
            m_octTiles = std::make_shared<SceneGroup>();
            e.Root()->AddItem(m_octTiles);
            m_frustum = std::make_shared<Frustum>();
            e.Root()->AddItem(m_frustum);
            e.Root()->AddItem(m_player->GetPlayerGroup());
            Camera::Fly fly;
            Camera::Fly dfly;
            
            m_player->Initialize(m_level);
            fly.pos = dfly.pos = m_player->Pos();
            fly.dir = dfly.dir = m_player->Dir();
            e.DrawCam().SetFly(dfly);
            e.ViewCam().SetFly(fly);
            m_octTiles->BeforeDraw([this](DrawContext& ctx) { ctx.m_pgm = BGFX_INVALID_HANDLE; return true; });
            m_physics = std::make_shared<Physics>();
            Engine::Inst().AddExternalDraw(&m_connectionLogic);
        }
   
        ctx.m_physics = m_physics;
        if (ctx.m_physics)
            ctx.m_physics->Step(ctx);

        if (ctx.m_pickedItem != nullptr)
        {
            std::shared_ptr<LegoBrick> pBrick = 
                std::dynamic_pointer_cast<LegoBrick>(ctx.m_pickedItem);
            if (m_pPickedBrick != pBrick && 
                m_pPickedBrick != nullptr)
            {
                m_pPickedBrick->SetPickData(-1);
            }
            if (pBrick != nullptr)
            {
                pBrick->SetPickData(ctx.m_pickedVal);
            }

            m_pPickedBrick = pBrick;
        }

        m_frustum->SetEnabled(m_player->InspectMode());
       
        m_player->Update(ctx, m_level);

        if (m_player->InspectMode())
        {
            auto octtile = m_octTileSelection.TileFromPos(e.DrawCam().GetFly().pos);
            if (octtile != nullptr)
            {
                g_inLoc = octtile->GetLoc();
            }
            else
                g_inLoc = Loc(0, 0, 0);
        }
        auto &cam = e.ViewCam();
        Camera::Fly fly = cam.GetFly();        
        const float playerHeadHeight = 0.01f;
        const float playerBodyWidth = playerHeadHeight;

        Vec3f boundsExt(0.01f, 0.01f, 0.01f);
        AABoxf playerbounds(fly.pos - boundsExt, fly.pos + boundsExt);

        if (!isPaused)
        {
            m_octTiles->Clear();
            m_octTileSelection.Update(e, ctx, playerbounds);
            m_octTileSelection.AddTilesToGroup(m_octTiles);
        }

        std::shared_ptr<OctTile> tile = m_octTileSelection.TileFromPos(fly.pos);
        m_octTileSelection.GetNearFarMidDist(ctx.m_nearfar);
        e.ViewCam().SetNearFar(ctx.m_nearfar[0], ctx.m_nearfar[2]);

        {
            Matrix44f mat0 = cam.GetPerspectiveMatrix(ctx.m_nearfar[0], ctx.m_nearfar[1]) *
                cam.ViewMatrix();
            invert(mat0);
            Vec4f nearWsPt, farWsPt;
            xform(nearWsPt, mat0, Vec4f(0, 0, 0, 1));
            Matrix44f mat1 = cam.GetPerspectiveMatrix(ctx.m_nearfar[1], ctx.m_nearfar[2]) *
                cam.ViewMatrix();
            invert(mat1);
            xform(farWsPt, mat1, Vec4f(0, 0, 1, 1));
            nearWsPt /= nearWsPt[3];
            farWsPt /= farWsPt[3];
            Vec3f dir = Point3f(farWsPt[0], farWsPt[1], farWsPt[2]) -
                Point3f(nearWsPt[0], nearWsPt[1], nearWsPt[2]);
            normalize(dir);
            Loc hitLoc(0,0,0);
            Vec3i hitpt;

            if (m_octTileSelection.Intersects(Point3f(nearWsPt[0], nearWsPt[1], nearWsPt[2]), dir, hitLoc, hitpt))
            {
                AABox aabb = hitLoc.GetBBox();
                Vec3f extents = (aabb.mMax - aabb.mMin);
                const int tsz = 256;
                float scl = extents[0] / (float)tsz; 
                
                Point3f offset = Vec3f(hitpt[0], hitpt[1], hitpt[2]) * scl + aabb.mMin;

                g_hitLoc = hitLoc;
                bool test;
                Matrix44f vp = cam.GetPerspectiveMatrix(ctx.m_nearfar[0], ctx.m_nearfar[1])*
                    cam.ViewMatrix();

                g_hitDist = dot(offset - fly.pos, dir);
            }
        }


        if (!m_player->InspectMode())
        {
            std::shared_ptr<OctTile> tile = m_octTileSelection.TileFromPos(fly.pos);
            if (tile == nullptr || tile->GetReadyState() < 3)
            {
            }
            else
            {                               
            }
        }
        else
        {
        }

    }
   

    void World::Layout(int w, int h)
    {
        m_width = w;
        m_height = h;
    }
    
    struct Palette
    {
        float v;
        unsigned char r;
        unsigned char g;
        unsigned char b;

        Palette(float _V, unsigned char _r, unsigned char _g, unsigned char _b) :
            v(_V),
            r(_r),
            g(_g),
            b(_b)
        {  }
    };


    

    World::~World()
    {

    }

}
