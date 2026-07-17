export default function Footer() {
  const year = new Date().getFullYear()

  return (
    <footer className="border-t border-border py-6 px-4 mt-auto">
      <div className="max-w-7xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-2 text-sm text-text-2">
        <p>&copy; {year} Ticketera. All rights reserved.</p>
        <a
          href="/"
          className="text-brand-1 hover:text-brand-2 transition-colors duration-[var(--dur-micro)]"
        >
          Powered by Ticketera
        </a>
      </div>
    </footer>
  )
}
