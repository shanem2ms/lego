#pragma once

#include <map>
#include <set>
#include "SceneItem.h"
#include "PartDefs.h"

class btDefaultMotionState;
class btRigidBody;

namespace sam
{
    class BrickManager;
    class Brick;
    class LegoBrick : public SceneGroup
    {        
        PartId m_partid;
        Brick* m_pBrick;
        int m_paletteIdx;
        bool m_showConnectors;
        int m_connectorPickIdx;
        std::shared_ptr<SceneItem> m_connectorPickWidget;
        std::shared_ptr<btDefaultMotionState> m_initialState;
        std::shared_ptr<btRigidBody> m_rigidBody;
    public:
        LegoBrick(const PartId& partstr, int paletteIdx, bool showConnectors = false);
        virtual ~LegoBrick();
        void Decomission(DrawContext& ctx) override;
        void Initialize(DrawContext& nvg) override;
        void Draw(DrawContext& ctx) override;
        void SetPickData(float data);
        Brick* GetBrick() { return m_pBrick; }
        void CreateBulletMesh();
        int GetHighlightedConnector() const
        { return m_connectorPickIdx; }
    };
}