import { cn } from '@/lib/utils'

function Bone({ className }: { className?: string }) {
  return (
    <div className={cn('bg-gray-200 dark:bg-gray-700 rounded animate-pulse', className)} />
  )
}

export function AppSkeleton() {
  return (
    <div className="flex h-screen w-screen bg-white dark:bg-[#1a1a1c]">
      {/* Sidebar skeleton */}
      <div className="w-64 border-r border-gray-100 dark:border-gray-800 flex flex-col p-4 gap-4 shrink-0">
        <Bone className="h-10 w-full rounded-lg" />
        <div className="flex flex-col gap-2 mt-4">
          <Bone className="h-8 w-full" />
          <Bone className="h-8 w-3/4" />
          <Bone className="h-8 w-5/6" />
          <Bone className="h-8 w-2/3" />
        </div>
        <div className="mt-auto">
          <Bone className="h-10 w-full rounded-lg" />
        </div>
      </div>

      {/* Main content skeleton */}
      <div className="flex-1 flex flex-col items-center justify-center p-8 gap-6">
        <Bone className="h-8 w-48 rounded-lg" />
        <Bone className="h-4 w-64" />
        <div className="flex gap-3 mt-4">
          <Bone className="h-10 w-24 rounded-full" />
          <Bone className="h-10 w-24 rounded-full" />
          <Bone className="h-10 w-24 rounded-full" />
        </div>
        <div className="mt-auto w-full max-w-4xl">
          <Bone className="h-14 w-full rounded-2xl" />
        </div>
      </div>
    </div>
  )
}
