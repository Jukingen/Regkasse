import { redirect } from 'next/navigation';

/** Legacy alias — canonical online-order inbox is `/orders/online`. */
export default function OnlineOrdersAliasPage() {
  redirect('/orders/online');
}
