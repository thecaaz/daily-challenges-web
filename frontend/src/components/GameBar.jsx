import React from 'react'
import './gamebar.css'

export default function GameBar() {
  // static placeholder values — can be wired to real data later
  const level = 7
  const xp = 420
  const xpForNext = 600
  const progress = Math.min(100, Math.round((xp / xpForNext) * 100))

  return (
    <header className="gamebar">
      <div className="gamebar-left">
        <div className="logo">🎮</div>
        <div>
          <div className="title">Daily Challenges</div>
          <div className="subtitle muted">Play, compete, repeat</div>
        </div>
      </div>

      <div className="gamebar-right">
        <div className="xp">
          <div className="xp-bar-outer">
            <div className="xp-bar-inner" style={{ width: `${progress}%` }} />
          </div>
          <div className="xp-meta muted">Level <strong>{level}</strong> • {xp}/{xpForNext} XP</div>
        </div>
        <div className="coins badge">💎 128</div>
      </div>
    </header>
  )
}
