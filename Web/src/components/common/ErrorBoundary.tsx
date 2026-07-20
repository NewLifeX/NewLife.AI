import { Component, type ReactNode, type ErrorInfo } from 'react'
import { Icon } from '@/components/common/Icon'
import i18n from '@/i18n'

interface ErrorBoundaryProps {
  /** 子节点 */
  children: ReactNode
  /** 自定义错误提示（默认显示通用错误 + 重试按钮） */
  fallback?: ReactNode
  /** 错误回调，用于上报日志 */
  onError?: (error: Error, info: ErrorInfo) => void
}

interface ErrorBoundaryState {
  hasError: Boolean
  error: Error | null
}

/**
 * 通用错误边界。捕获子组件渲染错误，防止整页白屏。
 * 显示友好错误提示并提供"重试"按钮（重置 error state 重新挂载子组件）。
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error }
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error('[ErrorBoundary] 捕获到渲染错误:', error, info.componentStack)
    this.props.onError?.(error, info)
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: null })
  }

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) return this.props.fallback

      return (
        <div className="flex flex-col items-center justify-center gap-3 p-8 text-center">
          <Icon name="error_outline" size="xl" className="text-[var(--color-text-tertiary)]" />
          <div>
            <p className="text-sm font-medium text-[var(--color-text-secondary)]">{i18n.t('chat.pageLoadFailed')}</p>
            <p className="text-xs text-[var(--color-text-tertiary)] mt-1">
              {this.state.error?.message ?? i18n.t('chat.unknownError')}
            </p>
          </div>
          <button
            onClick={this.handleRetry}
            className="px-4 py-1.5 text-xs font-medium rounded-lg bg-[color:var(--color-brand-500)] text-white hover:bg-[color:var(--color-brand-600)] transition-colors"
          >
            {i18n.t('common.retry')}
          </button>
        </div>
      )
    }

    return this.props.children
  }
}
