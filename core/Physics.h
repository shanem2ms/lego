#pragma once
#include <map>
#include <set>

class btDefaultCollisionConfiguration;
struct btDbvtBroadphase;
class btCollisionDispatcher;
class btDiscreteDynamicsWorld;
class btConstraintSolver;
class btRigidBody;
class btCollisionObject;

namespace sam
{
    struct DrawContext;
    class PhysicsDebugDraw;
    class Physics
    {
    public:
        Physics();

        static Physics& Inst();
        void Step(const DrawContext& ctx);

        btDiscreteDynamicsWorld* World()
        {
            return m_discreteDynamicsWorld.get();
        }
        static float WorldScale;

        void DebugRender(DrawContext& ctx);
        void AddRigidBody(btRigidBody* pRigidBody);
        void RemoveRigidBody(btRigidBody* pRigidBody);
        bool TestCollision(btCollisionObject* pObj);
        ~Physics();
    private:
        void Init();

        std::shared_ptr<btDefaultCollisionConfiguration> m_collisionConfig;
        std::shared_ptr<btDbvtBroadphase> m_broadPhase;
        std::shared_ptr<btCollisionDispatcher> m_collisionDispatcher;
        std::shared_ptr<btDiscreteDynamicsWorld> m_discreteDynamicsWorld;
        std::shared_ptr<btConstraintSolver> m_constraintSolver;
        std::shared_ptr<PhysicsDebugDraw> m_dbgPhysics;
        bool m_isInit;
    };
}