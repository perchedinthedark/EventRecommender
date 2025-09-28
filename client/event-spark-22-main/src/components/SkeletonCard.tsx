export default function SkeletonCard() {
  return (
    <div className="bg-white rounded-[24px] border border-slate-200 shadow-lg overflow-hidden animate-pulse">
      <div className="h-36 bg-blue-200" />
      <div className="p-5 space-y-3">
        <div className="h-5 w-3/4 bg-slate-200 rounded" />
        <div className="h-4 w-2/3 bg-slate-200 rounded" />
        <div className="h-4 w-1/2 bg-slate-200 rounded" />
      </div>
    </div>
  );
}