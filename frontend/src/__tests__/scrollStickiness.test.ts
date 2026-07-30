import { describe, expect, it } from 'vitest'
import { STICK_THRESHOLD_PX, isNearBottom } from '../areas/agents/scrollStickiness'

describe('isNearBottom', () => {
  it('is true when remaining distance is within the default threshold', () => {
    // scrollHeight 1000, clientHeight 100 → bottom at scrollTop 900
    expect(isNearBottom(900, 100, 1000)).toBe(true)
    expect(isNearBottom(900 - STICK_THRESHOLD_PX, 100, 1000)).toBe(true)
  })

  it('is false when the user has scrolled up past the threshold', () => {
    expect(isNearBottom(900 - STICK_THRESHOLD_PX - 1, 100, 1000)).toBe(false)
    expect(isNearBottom(0, 100, 1000)).toBe(false)
  })

  it('respects a custom threshold', () => {
    // distance = 1000 - scrollTop - 100; threshold 50 → sticky when scrollTop >= 850
    expect(isNearBottom(849, 100, 1000, 50)).toBe(false)
    expect(isNearBottom(850, 100, 1000, 50)).toBe(true)
  })
})
