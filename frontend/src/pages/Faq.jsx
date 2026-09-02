import { useState } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { staggerContainer, staggerItem, useReducedMotion } from '../lib/motion.js'

const faqGroups = [
  {
    id: 'compras-entradas',
    title: 'Compras y entradas',
    items: [
      {
        question: '¿Cómo compro entradas?',
        answer:
          'Elegí un evento del catálogo, seleccioná el tipo de entrada y la cantidad, y seguí el paso a paso para reservar y pagar. Al confirmar el pago, te enviamos las entradas con sus códigos QR al email del comprador.',
      },
      {
        question: '¿Necesito una cuenta para comprar?',
        answer:
          'No. Podés comprar como invitado: solo necesitás un email válido y tu DNI para completar la reserva.',
      },
      {
        question: '¿Cómo veo mis entradas?',
        answer:
          'Entrá a «Mis Entradas» desde el menú y buscá con el email y el DNI que usaste al comprar para consultar tus entradas. Las entradas se envían por email a la casilla del comprador: si no las recibiste, desde esa misma sección podés solicitar que te las reenvíen.',
      },
      {
        question: '¿Qué pasa si mi reserva expira antes de pagar?',
        answer:
          'La reserva tiene un tiempo límite para completar el pago. Si se vence, las entradas se liberan y vuelven a estar disponibles; solo tenés que volver a intentar la compra.',
      },
    ],
  },
  {
    id: 'pagos-devoluciones',
    title: 'Pagos y devoluciones',
    items: [
      {
        question: '¿Qué medios de pago aceptan?',
        answer:
          'Los pagos se procesan con Mercado Pago. Podés pagar con las tarjetas y los medios que Mercado Pago soporte.',
      },
      {
        question: '¿Por qué me redirigen a Mercado Pago para pagar?',
        answer:
          'El pago se procesa en la plataforma de Mercado Pago para que los datos de tu tarjeta nunca pasen por nuestro sistema.',
      },
      {
        question: '¿Puedo pedir una devolución?',
        answer:
          'Cada caso se evalúa de forma manual por el equipo de TicketStart. Si necesitás una devolución, escribinos y lo revisamos.',
      },
    ],
  },
  {
    id: 'eventos',
    title: 'Eventos',
    items: [
      {
        question: '¿Cómo sé que un evento es legítimo?',
        answer:
          'Todos los eventos pasan por un proceso de aprobación del equipo antes de publicarse en el catálogo.',
      },
      {
        question: '¿Quién organiza los eventos?',
        answer:
          'Los eventos son publicados y gestionados por organizadores independientes dentro de TicketStart.',
      },
    ],
  },
]

function FaqItem({ question, children }) {
  const [isOpen, setIsOpen] = useState(false)
  const prefersReducedMotion = useReducedMotion()
  const id = question
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-|-$)/g, '')

  return (
    <div className="glass-surface overflow-hidden">
      <button
        type="button"
        id={`faq-trigger-${id}`}
        aria-expanded={isOpen}
        aria-controls={`faq-panel-${id}`}
        onClick={() => setIsOpen((prev) => !prev)}
        className="flex w-full items-center justify-between gap-4 px-5 py-4 cursor-pointer text-left font-heading font-semibold text-text-1 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2"
      >
        {question}
        <svg
          className={`w-5 h-5 flex-shrink-0 text-brand-1 transition-transform duration-300 ${
            isOpen ? 'rotate-180' : ''
          }`}
          viewBox="0 0 20 20"
          fill="none"
          aria-hidden="true"
        >
          <path
            d="M5 7.5 10 12.5 15 7.5"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      </button>
      {/* Controlled accordion (APG disclosure pattern). Height animated with
          framer-motion so EVERY open/close animates identically — the CSS
          grid-rows 0fr→1fr trick only animated on the first open in Chromium
          when combined with native <details>. */}
      <AnimatePresence initial={false}>
        {isOpen && (
          <motion.div
            id={`faq-panel-${id}`}
            role="region"
            aria-labelledby={`faq-trigger-${id}`}
            initial={
              prefersReducedMotion ? { height: 'auto', opacity: 1 } : { height: 0, opacity: 0 }
            }
            animate={{ height: 'auto', opacity: 1 }}
            exit={prefersReducedMotion ? undefined : { height: 0, opacity: 0 }}
            transition={
              prefersReducedMotion
                ? { duration: 0 }
                : { duration: 0.3, ease: [0.4, 0, 0.2, 1] }
            }
            className="overflow-hidden"
          >
            <div className="px-5 pb-5 pt-4 border-t border-glass-border">
              <p className="text-text-2 leading-relaxed">{children}</p>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}

function FaqGroups() {
  return (
    <>
      {faqGroups.map((group) => (
        <motion.section
          key={group.id}
          variants={staggerItem}
          aria-labelledby={`faq-group-${group.id}`}
          className="space-y-4"
        >
          <h2
            id={`faq-group-${group.id}`}
            className="text-xl md:text-2xl font-heading font-semibold text-text-1"
          >
            {group.title}
          </h2>
          <div className="space-y-3">
            {group.items.map((item) => (
              <FaqItem key={item.question} question={item.question}>
                {item.answer}
              </FaqItem>
            ))}
          </div>
        </motion.section>
      ))}
    </>
  )
}

export default function Faq() {
  const prefersReducedMotion = useReducedMotion()

  return (
    <div className="relative -mt-16 bg-gradient-to-b from-cian/10 via-canvas to-amarillo/10">
      {/* Gradient background identical to the "Mis entradas" (TicketLookup)
          page. It starts at the very top, behind the translucent fixed navbar,
          so there is no white gap between the navbar and the page background. */}
      <div className="max-w-3xl mx-auto px-4 sm:px-6 pt-28 pb-12">
      <motion.header
        initial={prefersReducedMotion ? false : { opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
        className="text-center mb-6"
      >
        <h1 className="text-3xl font-display font-bold text-gris-oscuro mb-2">
          Preguntas frecuentes
        </h1>
        <p className="text-text-2 text-lg max-w-xl mx-auto">
          Resolvé tus dudas sobre compras, pagos y eventos.
        </p>
      </motion.header>

      {prefersReducedMotion ? (
        <div className="space-y-12">
          <FaqGroups />
        </div>
      ) : (
        <motion.div
          variants={staggerContainer}
          initial="initial"
          animate="animate"
          className="space-y-12"
        >
          <FaqGroups />
        </motion.div>
      )}
      </div>
    </div>
  )
}
