import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

export interface SkillItem {
  id: string
  icon: string
  label: string
  active?: boolean
}

interface SkillBarProps {
  skills: SkillItem[]
  onSkillClick?: (id: string) => void
  className?: string
}

export function SkillBar({ skills, onSkillClick, className }: SkillBarProps) {
  return (
    <div className={cn('flex items-center space-x-1 overflow-x-auto no-scrollbar', className)}>
      {skills.map((skill) => (
        <button
          key={skill.id}
          onClick={() => onSkillClick?.(skill.id)}
          className={cn(
            'flex items-center space-x-1 px-2 py-1.5 rounded-lg text-xs font-medium transition-colors flex-shrink-0 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
            skill.active
              ? 'bg-blue-50 dark:bg-blue-900/30 text-blue-600 dark:text-blue-300 border border-blue-100 dark:border-blue-800'
              : 'text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700',
          )}
        >
          <Icon name={skill.icon} size="sm" />
          <span>{skill.label}</span>
        </button>
      ))}
    </div>
  )
}
