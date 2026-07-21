import { redirect } from 'next/navigation';

/** Legacy alias — canonical route is `/pricing-rules`. */
export default function PriceRulesRedirectPage() {
  redirect('/pricing-rules');
}
