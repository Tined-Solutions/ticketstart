import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import GradientHero from '../components/ui/GradientHero.jsx'
import Button from '../components/Button.jsx'
import { staggerContainer, staggerItem } from '../lib/motion.js'

export default function Home() {
  return (
    <div className="home-page">
      {/* Full-viewport GradientHero */}
      <GradientHero
        imageUrl={null}
        title={
          <span className="bg-gradient-to-r from-brand-1 to-brand-2 bg-clip-text text-transparent">
            Ticketera Online
          </span>
        }
        subtitle="La plataforma mas simple para descubrir eventos, reservar entradas y gestionar tus propios shows."
        cta={
          <Link to="/events">
            <Button variant="gradient" size="lg">
              Ver catalogo de eventos
            </Button>
          </Link>
        }
      />

      {/* Features section */}
      <motion.section
        variants={staggerContainer}
        initial="initial"
        whileInView="animate"
        viewport={{ once: true, margin: '-100px' }}
        className="max-w-5xl mx-auto px-4 py-20"
      >
        <motion.h2
          variants={staggerItem}
          className="text-3xl md:text-4xl font-display font-bold text-text-1 text-center mb-12"
        >
          Que podes hacer?
        </motion.h2>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
          {[
            { title: 'Explorar', desc: 'Descubri eventos publicados por organizadores.' },
            { title: 'Reservar', desc: 'Asegura tus entradas con confianza.' },
            { title: 'Pagar', desc: 'Paga con Mercado Pago de forma segura.' },
            { title: 'Gestionar', desc: 'Organiza y administra tus propios eventos.' },
          ].map(({ title, desc }) => (
            <motion.div
              key={title}
              variants={staggerItem}
              className="glass-surface p-6 text-center"
            >
              <h3 className="text-lg font-heading font-semibold text-text-1 mb-2">
                {title}
              </h3>
              <p className="text-text-2 text-sm">{desc}</p>
            </motion.div>
          ))}
        </div>
      </motion.section>
    </div>
  )
}
