import { useRef, useState, useEffect, useCallback } from 'react'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { SkillPopover } from './SkillPopover'

export interface SkillItem {
  id: string
  icon: string
  label: string
  active?: boolean
}

interface SkillBarProps {
  skills: SkillItem[]
  onSkillClick?: (id: string) => void
  /** "more" popover state & callbacks, managed by parent */
  popoverOpen?: boolean
  onPopoverToggle?: () => void
  onPopoverClose?: () => void
  popoverOptions?: { id: string; icon: string; iconBg: string; iconColor: string; label: string; description: string; active?: boolean }[]
  onPopoverSelect?: (id: string) => void
  className?: string
}

const GAP = 4

export function SkillBar({
  skills,
  onSkillClick,
  popoverOpen,
  onPopoverToggle,
  onPopoverClose,
  popoverOptions,
  onPopoverSelect,
  className,
}: SkillBarProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const btnWidths = useRef<Map<string, number>>(new Map())
  const measureRef = useRef<HTMLDivElement>(null)
  const moreButtonRef = useRef<HTMLButtonElement>(null)
  const [visibleCount, setVisibleCount] = useState<number>(-1) // -1 = not measured

  const attachSkill = skills.find((s) => s.id === 'attach')
  const moreSkill = skills.find((s) => s.id === 'more')
  const middleSkills = skills.filter((s) => s.id !== 'attach' && s.id !== 'more')

  // Measure all buttons in a hidden row, then decide how many fit
  const calcVisible = useCallback(() => {
    const container = containerRef.current
    const measure = measureRef.current
    if (!container || !measure) return

    const containerWidth = container.clientWidth
    // Read widths from the hidden measurement row
    const children = Array.from(measure.children) as HTMLElement[]
    const widths = new Map<string, number>()
    children.forEach((el) => {
      const id = el.dataset.id
      if (id) widths.set(id, el.offsetWidth)
    })
    btnWidths.current = widths

    let used = 0
    const attachW = widths.get('attach') ?? 0
    const moreW = widths.get('more') ?? 0
    if (attachW) used += attachW + GAP

    // First: can all middle buttons fit without "more"?
    let total = used
    let allFit = true
    for (const skill of middleSkills) {
      const w = widths.get(skill.id) ?? 0
      total += w + GAP
      if (total > containerWidth) { allFit = false; break }
    }
    if (allFit) {
      setVisibleCount(middleSkills.length)
      return
    }

    // Not all fit — reserve space for "more" button
    let count = 0
    let usedWithMore = used
    for (const skill of middleSkills) {
      const w = widths.get(skill.id) ?? 0
      if (usedWithMore + w + GAP + moreW + GAP <= containerWidth) {
        usedWithMore += w + GAP
        count++
      } else {
        break
      }
    }
    setVisibleCount(count)
  }, [skills]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const container = containerRef.current
    if (!container) return
    const ro = new ResizeObserver(() => calcVisible())
    ro.observe(container)
    // Double rAF to ensure fonts/icons are rendered
    requestAnimationFrame(() => requestAnimationFrame(calcVisible))
    return () => ro.disconnect()
  }, [calcVisible])

  const measured = visibleCount >= 0
  const safeCount = measured ? visibleCount : middleSkills.length
  const showMore = measured && safeCount < middleSkills.length

  // Only show overflowed skills in the popover
  const visibleIds = new Set(middleSkills.slice(0, safeCount).map((s) => s.id))
  const overflowedSkills = middleSkills.slice(safeCount)
  const filteredPopoverOptions = popoverOptions?.filter((opt) => !visibleIds.has(opt.id))

  // If an active skill is in the overflow area, highlight the "more" button
  const hasActiveOverflow = overflowedSkills.some((s) => s.active)

  const btnClass = (skill: SkillItem) =>
    cn(
      'flex items-center space-x-1 px-2 py-1.5 rounded-lg text-xs font-medium transition-colors flex-shrink-0 whitespace-nowrap border focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[color:var(--color-brand-500)]/50',
      skill.active
        ? 'bg-[color:var(--color-brand-50)] text-[color:var(--color-brand-700)] dark:text-[color:var(--color-brand-300)] border-[color:var(--color-brand-100)] dark:border-[color:var(--color-brand-800)]'
        : 'border-transparent text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-2)]',
    )

  return (
    <div ref={containerRef} className={cn('flex-1 min-w-0 relative', className)}>
      {/* Hidden measurement row — all buttons rendered invisibly */}
      <div
        ref={measureRef}
        className="flex items-center gap-1 absolute top-0 left-0 invisible pointer-events-none whitespace-nowrap"
        aria-hidden
      >
        {attachSkill && (
          <button data-id="attach" className={btnClass(attachSkill)} tabIndex={-1}>
            <Icon name={attachSkill.icon} size="base" />
            {attachSkill.label && <span>{attachSkill.label}</span>}
          </button>
        )}
        {middleSkills.map((skill) => (
          <button key={skill.id} data-id={skill.id} className={btnClass(skill)} tabIndex={-1}>
            <Icon name={skill.icon} size="base" />
            <span>{skill.label}</span>
          </button>
        ))}
        <button data-id="more" className={btnClass(moreSkill ?? { id: 'more', icon: 'grid_view', label: '' })} tabIndex={-1}>
          <Icon name={moreSkill?.icon ?? 'grid_view'} size="base" />
          {moreSkill?.label && <span>{moreSkill.label}</span>}
        </button>
      </div>

      {/* Visible buttons */}
      <div className="flex items-center gap-1">
        {attachSkill && (
          <button onClick={() => onSkillClick?.('attach')} data-testid="attach-button" className={btnClass(attachSkill)}>
            <Icon name={attachSkill.icon} size="base" />
            {attachSkill.label && <span>{attachSkill.label}</span>}
          </button>
        )}
        {middleSkills.slice(0, safeCount).map((skill) => (
          <button key={skill.id} onClick={() => onSkillClick?.(skill.id)} className={btnClass(skill)}>
            <Icon name={skill.icon} size="base" />
            <span>{skill.label}</span>
          </button>
        ))}
        {showMore && (
          <div className="relative">
            <button ref={moreButtonRef} onClick={onPopoverToggle} className={btnClass({ ...(moreSkill ?? { id: 'more', icon: 'grid_view', label: '' }), active: hasActiveOverflow })}>
              <Icon name={moreSkill?.icon ?? 'grid_view'} size="base" />
              {moreSkill?.label && <span>{moreSkill.label}</span>}
            </button>
            <SkillPopover
              open={!!popoverOpen}
              onSelect={(id) => onPopoverSelect?.(id)}
              onClose={() => onPopoverClose?.()}
              options={filteredPopoverOptions}
              anchorRef={moreButtonRef}
            />
          </div>
        )}
      </div>
    </div>
  )
}
